using FluentValidation;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Application.Planning {
    public abstract class PlanningAssistHandlerBase {
        protected readonly IAiCompletionService Ai;
        protected readonly IEngagementAccessGuard Access;
        protected readonly IEngagementRepository Engagements;
        protected readonly IClientLookup Clients;
        protected readonly ITrialBalanceRepository TrialBalances;
        protected readonly IActivityRecorder Activity;

        protected PlanningAssistHandlerBase(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, ITrialBalanceRepository trialBalances,
            IActivityRecorder activity) {
            Ai = ai;
            Access = access;
            Engagements = engagements;
            Clients = clients;
            TrialBalances = trialBalances;
            Activity = activity;
        }

        protected async Task<Result<AiProposalResult>> RunAsync(Guid engagementId,
            AuditAiCapability capability, string instruction, string userPrompt,
            string activityAction, string activitySummary, CancellationToken ct) {
            await Access.EnsureMemberAsync(engagementId, ct);

            var overview = EngagementAiContext.OverviewAsync(engagementId, Engagements,
                Clients, ct);
            var trialBalance = EngagementAiContext.TrialBalanceAsync(engagementId,
                TrialBalances, ct);

            await Task.WhenAll(overview, trialBalance);

            if (overview.Result.Count == 0) {
                return Result<AiProposalResult>.Error("The engagement was not found.");
            }

            var context = new List<AiDocument>(overview.Result);

            if (trialBalance.Result is not null) {
                context.Add(trialBalance.Result);
            }

            var completion = await Ai.CompleteAsync(AuditAiPrompts.Workload(capability,
                instruction, userPrompt, context, engagementId), ct);

            await Activity.RecordAsync(new ActivityEntry("Audit", activityAction,
                "Engagement", engagementId, activitySummary, engagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(capability, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AssistAuditPlanCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string? FocusArea { get; set; }
    }

    public class AssistAuditPlanCommandValidator : AbstractValidator<AssistAuditPlanCommand> {
        public AssistAuditPlanCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.FocusArea).MaximumLength(500);
        }
    }

    public class AssistAuditPlanCommandHandler : PlanningAssistHandlerBase,
        IRequestHandler<AssistAuditPlanCommand, Result<AiProposalResult>> {
        public AssistAuditPlanCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, ITrialBalanceRepository trialBalances,
            IActivityRecorder activity)
            : base(ai, access, engagements, clients, trialBalances, activity) { }

        public Task<Result<AiProposalResult>> HandleAsync(AssistAuditPlanCommand request,
            CancellationToken ct) =>
            RunAsync(request.EngagementId, AuditAiCapabilities.PlanAssistance,
                "Propose the audit plan for this engagement: scope, objectives and overall " +
                "strategy, plus the significant account balances and areas the plan should " +
                "cover and why. Where the plan already records something, say whether it " +
                "still holds rather than replacing it silently.",
                request.FocusArea is null
                    ? "Help me plan this engagement."
                    : $"Help me plan this engagement, focusing on: {request.FocusArea}",
                "ai.plan_assistance", "generated AI audit planning assistance.", ct);
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AssistMaterialityCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class AssistMaterialityCommandValidator
        : AbstractValidator<AssistMaterialityCommand> {
        public AssistMaterialityCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class AssistMaterialityCommandHandler : PlanningAssistHandlerBase,
        IRequestHandler<AssistMaterialityCommand, Result<AiProposalResult>> {
        public AssistMaterialityCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, ITrialBalanceRepository trialBalances,
            IActivityRecorder activity)
            : base(ai, access, engagements, clients, trialBalances, activity) { }

        public Task<Result<AiProposalResult>> HandleAsync(AssistMaterialityCommand request,
            CancellationToken ct) =>
            RunAsync(request.EngagementId, AuditAiCapabilities.MaterialityAssistance,
                "Discuss materiality for this engagement: which benchmarks suit this entity " +
                "and why, what percentage ranges are commonly applied to each, and what that " +
                "implies for overall materiality, performance materiality and the clearly " +
                "trivial threshold. Compute figures only from amounts present in the context; " +
                "where the benchmark amount is not available, say so and stop rather than " +
                "estimating it. The threshold set is the engagement partner's judgment.",
                "Help me set materiality for this engagement.",
                "ai.materiality_assistance", "generated AI materiality assistance.", ct);
    }
}
