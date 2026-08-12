using FluentValidation;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.AI.Application.Portfolio {
    /// <summary>
    /// Which engagements a cross-engagement request may see. It is the same rule the
    /// per-engagement guard applies, resolved once for the whole set: the engagements the caller
    /// is assigned to, or every engagement in the organization when the caller holds
    /// organization-level oversight. Everything below is already scoped to the caller's own
    /// organization by the repository, so no query can reach another tenant.
    /// </summary>
    internal static class PortfolioScope {
        public static async Task<IReadOnlyList<DomainEngagement>> VisibleAsync(Guid? clientId,
            IEngagementRepository engagements, ITeamRepository team,
            ICurrentUserAccessor currentUser, CancellationToken ct) {
            var user = currentUser.Require();
            var all = await engagements.ListAsync(clientId, ct);

            if (user.Role >= OrganizationRole.Admin) {
                return all;
            }

            var assigned = (await team.ListEngagementIdsForUserAsync(user.UserId, ct)).ToHashSet();

            return [.. all.Where(engagement => assigned.Contains(engagement.Id))];
        }
    }

    internal static class PortfolioContext {
        public static async Task<List<AiDocument>> BuildAsync(
            IReadOnlyList<DomainEngagement> engagements, IClientLookup clients,
            IRiskRepository risks, IFindingRepository findings, CancellationToken ct) {
            var names = await clients.GetNamesAsync(
                engagements.Select(engagement => engagement.ClientId).Distinct(), ct);

            var documents = new List<AiDocument> {
                new("Engagement portfolio", string.Join('\n', engagements.Select(engagement =>
                    $"- {engagement.Name} | client: " +
                    $"{names.GetValueOrDefault(engagement.ClientId, "Unknown")} | " +
                    $"type: {engagement.Type} | status: {engagement.Status} | " +
                    $"period: {engagement.PeriodStart} to {engagement.PeriodEnd}")))
            };

            // Each engagement's risks and findings are separate round trips; they do not depend
            // on one another, so the whole portfolio is read in one pass rather than serially.
            var perEngagement = await Task.WhenAll(engagements.Select(async engagement => {
                var riskList = risks.ListAsync(engagement.Id, ct);
                var findingList = findings.ListAsync(engagement.Id, ct);

                await Task.WhenAll(riskList, findingList);

                return (engagement, Risks: riskList.Result, Findings: findingList.Result);
            }));

            foreach (var (engagement, riskList, findingList) in perEngagement) {
                if (riskList.Count == 0 && findingList.Count == 0) {
                    continue;
                }

                documents.Add(new AiDocument($"{engagement.Name} — risks and findings",
                    string.Join('\n', riskList
                        .Select(risk => $"RISK [{risk.Level}] {risk.Title}: {risk.Description}")
                        .Concat(findingList.Select(finding =>
                            $"FINDING [{finding.Severity}/{finding.Status}] {finding.Title}: " +
                            $"{finding.Description}")))));
            }

            return documents;
        }
    }

    public abstract class PortfolioHandlerBase {
        protected readonly IAiCompletionService Ai;
        protected readonly IEngagementRepository Engagements;
        protected readonly ITeamRepository Team;
        protected readonly IClientLookup Clients;
        protected readonly IRiskRepository Risks;
        protected readonly IFindingRepository Findings;
        protected readonly ICurrentUserAccessor CurrentUser;
        protected readonly IActivityRecorder Activity;

        protected PortfolioHandlerBase(IAiCompletionService ai,
            IEngagementRepository engagements, ITeamRepository team, IClientLookup clients,
            IRiskRepository risks, IFindingRepository findings,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            Ai = ai;
            Engagements = engagements;
            Team = team;
            Clients = clients;
            Risks = risks;
            Findings = findings;
            CurrentUser = currentUser;
            Activity = activity;
        }

        protected async Task<List<AiDocument>?> ContextAsync(Guid? clientId,
            CancellationToken ct) {
            var visible = await PortfolioScope.VisibleAsync(clientId, Engagements, Team,
                CurrentUser, ct);

            return visible.Count == 0
                ? null
                : await PortfolioContext.BuildAsync(visible, Clients, Risks, Findings, ct);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AnalyzePortfolioCommand : ICommand<Result<AiProposalResult>> {
        /// <summary>Narrows the analysis to one client; null covers every visible engagement.</summary>
        public Guid? ClientId { get; set; }

        public string? Question { get; set; }
    }

    public class AnalyzePortfolioCommandValidator : AbstractValidator<AnalyzePortfolioCommand> {
        public AnalyzePortfolioCommandValidator() {
            RuleFor(x => x.Question).MaximumLength(2000);
        }
    }

    /// <summary>
    /// Firm and client intelligence: what recurs across engagements, how a client's risk picture
    /// has moved period over period, and where the same finding keeps coming back.
    /// </summary>
    public class AnalyzePortfolioCommandHandler : PortfolioHandlerBase,
        IRequestHandler<AnalyzePortfolioCommand, Result<AiProposalResult>> {
        public AnalyzePortfolioCommandHandler(IAiCompletionService ai,
            IEngagementRepository engagements, ITeamRepository team, IClientLookup clients,
            IRiskRepository risks, IFindingRepository findings,
            ICurrentUserAccessor currentUser, IActivityRecorder activity)
            : base(ai, engagements, team, clients, risks, findings, currentUser, activity) { }

        public async Task<Result<AiProposalResult>> HandleAsync(AnalyzePortfolioCommand request,
            CancellationToken ct) {
            var context = await ContextAsync(request.ClientId, ct);

            if (context is null) {
                return Result<AiProposalResult>.Error(
                    "There are no engagements you can see for this analysis.");
            }

            var completion = await Ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.PortfolioIntelligence,
                "Analyze this set of engagements as a portfolio: findings that recur across " +
                "engagements or periods, risks that keep being identified, how a client's " +
                "risk picture has moved between periods, and where engagements of the same " +
                "type reached different conclusions. Say which engagements each observation " +
                "comes from. The set you are given is the whole set you may consider.",
                request.Question ?? "What patterns run across these engagements?",
                context, clientId: request.ClientId), ct);

            await Activity.RecordAsync(new ActivityEntry("Audit", "ai.portfolio_intelligence",
                "Organization", CurrentUser.RequireOrganizationId(),
                "generated an AI portfolio analysis across engagements.", null), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.PortfolioIntelligence, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Manage)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class GeneratePortfolioReportCommand : ICommand<Result<AiProposalResult>> {
        public Guid? ClientId { get; set; }
        public string? Instruction { get; set; }
    }

    public class GeneratePortfolioReportCommandValidator
        : AbstractValidator<GeneratePortfolioReportCommand> {
        public GeneratePortfolioReportCommandValidator() {
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    /// <summary>
    /// Reporting above the level of a single engagement — a client report or a firm-level
    /// summary. It is returned as a proposal rather than stored: it belongs to no one
    /// engagement, so there is no engagement review workflow to enter.
    /// </summary>
    public class GeneratePortfolioReportCommandHandler : PortfolioHandlerBase,
        IRequestHandler<GeneratePortfolioReportCommand, Result<AiProposalResult>> {
        public GeneratePortfolioReportCommandHandler(IAiCompletionService ai,
            IEngagementRepository engagements, ITeamRepository team, IClientLookup clients,
            IRiskRepository risks, IFindingRepository findings,
            ICurrentUserAccessor currentUser, IActivityRecorder activity)
            : base(ai, engagements, team, clients, risks, findings, currentUser, activity) { }

        public async Task<Result<AiProposalResult>> HandleAsync(
            GeneratePortfolioReportCommand request, CancellationToken ct) {
            var context = await ContextAsync(request.ClientId, ct);

            if (context is null) {
                return Result<AiProposalResult>.Error(
                    "There are no engagements you can see for this report.");
            }

            var completion = await Ai.CompleteAsync(AuditAiPrompts.ReportWorkload(
                AuditAiCapabilities.PortfolioReport,
                request.ClientId is null
                    ? "Produce a firm-level summary across these engagements: coverage, the " +
                      "themes running through the findings, and where attention is most needed."
                    : "Produce a client-level report across this client's engagements: what " +
                      "was covered in each period, what was found, what recurs, and what " +
                      "remains open.",
                request.Instruction ?? "Produce the report.", context,
                clientId: request.ClientId), ct);

            await Activity.RecordAsync(new ActivityEntry("Audit", "ai.portfolio_report",
                "Organization", CurrentUser.RequireOrganizationId(),
                "generated an AI report across engagements.", null), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.PortfolioReport, completion));
        }
    }
}
