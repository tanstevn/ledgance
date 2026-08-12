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

namespace Ledgance.Audit.AI.Application.Analysis {
    public abstract class EngagementAnalysisHandlerBase {
        protected readonly IAiCompletionService Ai;
        protected readonly IEngagementAccessGuard Access;
        protected readonly IEngagementRepository Engagements;
        protected readonly IClientLookup Clients;
        protected readonly IActivityRecorder Activity;

        protected EngagementAnalysisHandlerBase(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity) {
            Ai = ai;
            Access = access;
            Engagements = engagements;
            Clients = clients;
            Activity = activity;
        }

        protected async Task<Result<AiProposalResult>> RunAsync(Guid engagementId,
            AuditAiCapability capability, string instruction, string userPrompt,
            List<AiDocument> context, string activityAction, string activitySummary,
            CancellationToken ct) {
            var completion = await Ai.CompleteAsync(AuditAiPrompts.Workload(capability,
                instruction, userPrompt, context, engagementId), ct);

            await Activity.RecordAsync(new ActivityEntry("Audit", activityAction,
                "Engagement", engagementId, activitySummary, engagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(capability, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AnalyzeRisksCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class AnalyzeRisksCommandValidator : AbstractValidator<AnalyzeRisksCommand> {
        public AnalyzeRisksCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class AnalyzeRisksCommandHandler : EngagementAnalysisHandlerBase,
        IRequestHandler<AnalyzeRisksCommand, Result<AiProposalResult>> {
        private readonly IRiskRepository _risks;
        private readonly IProcedureRepository _procedures;
        private readonly ITrialBalanceRepository _trialBalances;

        public AnalyzeRisksCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity, IRiskRepository risks,
            IProcedureRepository procedures, ITrialBalanceRepository trialBalances)
            : base(ai, access, engagements, clients, activity) {
            _risks = risks;
            _procedures = procedures;
            _trialBalances = trialBalances;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AnalyzeRisksCommand request,
            CancellationToken ct) {
            await Access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                Engagements, Clients, ct);

            foreach (var document in new[] {
                await EngagementAiContext.RisksAsync(request.EngagementId, _risks, ct),
                await EngagementAiContext.ProceduresAsync(request.EngagementId, _procedures, ct),
                await EngagementAiContext.TrialBalanceAsync(request.EngagementId,
                    _trialBalances, ct)
            }) {
                if (document is not null) {
                    context.Add(document);
                }
            }

            return await RunAsync(request.EngagementId, AuditAiCapabilities.RiskAnalysis,
                "Perform a cross-document risk analysis of this engagement: assess whether " +
                "the identified risks are complete and correctly rated given the trial " +
                "balance and materiality, whether each significant risk has an adequate " +
                "response, and where the risk assessment and the numbers disagree.",
                "Analyze this engagement's risk assessment.",
                context, "ai.risk_analysis", "generated an AI risk analysis.", ct);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class DetectAnomaliesCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class DetectAnomaliesCommandValidator : AbstractValidator<DetectAnomaliesCommand> {
        public DetectAnomaliesCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class DetectAnomaliesCommandHandler : EngagementAnalysisHandlerBase,
        IRequestHandler<DetectAnomaliesCommand, Result<AiProposalResult>> {
        private readonly ITrialBalanceRepository _trialBalances;

        public DetectAnomaliesCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity,
            ITrialBalanceRepository trialBalances)
            : base(ai, access, engagements, clients, activity) {
            _trialBalances = trialBalances;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(DetectAnomaliesCommand request,
            CancellationToken ct) {
            await Access.EnsureMemberAsync(request.EngagementId, ct);

            var trialBalance = await EngagementAiContext.TrialBalanceAsync(
                request.EngagementId, _trialBalances, ct);

            if (trialBalance is null) {
                return Result<AiProposalResult>.Error(
                    "Import a trial balance before running anomaly detection.");
            }

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                Engagements, Clients, ct);
            context.Add(trialBalance);

            return await RunAsync(request.EngagementId, AuditAiCapabilities.AnomalyDetection,
                "Examine the trial balance for anomalies an auditor should investigate: " +
                "unusual balances or signs, accounts inconsistent with their type, amounts " +
                "unusual relative to materiality, round-number patterns, and imbalances. " +
                "Rank by significance and say why each matters.",
                "Detect anomalies in this engagement's trial balance.",
                context, "ai.anomaly_detection", "AI anomaly detection was run.", ct);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AssistReviewCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class AssistReviewCommandValidator : AbstractValidator<AssistReviewCommand> {
        public AssistReviewCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class AssistReviewCommandHandler : EngagementAnalysisHandlerBase,
        IRequestHandler<AssistReviewCommand, Result<AiProposalResult>> {
        private readonly IRiskRepository _risks;
        private readonly IProcedureRepository _procedures;
        private readonly IWorkingPaperRepository _papers;
        private readonly IFindingRepository _findings;

        public AssistReviewCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity, IRiskRepository risks,
            IProcedureRepository procedures, IWorkingPaperRepository papers,
            IFindingRepository findings)
            : base(ai, access, engagements, clients, activity) {
            _risks = risks;
            _procedures = procedures;
            _papers = papers;
            _findings = findings;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AssistReviewCommand request,
            CancellationToken ct) {
            await Access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                Engagements, Clients, ct);

            foreach (var document in new[] {
                await EngagementAiContext.RisksAsync(request.EngagementId, _risks, ct),
                await EngagementAiContext.ProceduresAsync(request.EngagementId, _procedures, ct),
                await EngagementAiContext.FindingsAsync(request.EngagementId, _findings, ct)
            }) {
                if (document is not null) {
                    context.Add(document);
                }
            }

            var papers = await _papers.ListAsync(request.EngagementId, ct);

            if (papers.Count > 0) {
                context.Add(new AiDocument("Working papers", string.Join('\n', papers
                    .Select(paper => $"- [{paper.Status}] {paper.Reference} {paper.Title} " +
                        $"(open review notes: {paper.OpenNoteCount})"))));
            }

            return await RunAsync(request.EngagementId, AuditAiCapabilities.ReviewAssistance,
                "Act as a reviewing partner preparing for sign-off: identify incomplete or " +
                "unapproved work, risks without responsive procedures, open findings and " +
                "review notes, and inconsistencies between working papers, findings and the " +
                "plan. Produce a prioritized review checklist.",
                "Review this engagement's completeness and quality.",
                context, "ai.review_assistance", "generated an AI review assessment.", ct);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AnalyzeEvidenceCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class AnalyzeEvidenceCommandValidator : AbstractValidator<AnalyzeEvidenceCommand> {
        public AnalyzeEvidenceCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    /// <summary>
    /// Evidence coverage read across the whole engagement rather than one document at a time:
    /// which risks and procedures have evidence behind them and which do not.
    /// </summary>
    public class AnalyzeEvidenceCommandHandler : EngagementAnalysisHandlerBase,
        IRequestHandler<AnalyzeEvidenceCommand, Result<AiProposalResult>> {
        private readonly IRiskRepository _risks;
        private readonly IProcedureRepository _procedures;
        private readonly IEvidenceRepository _evidence;
        private readonly IWorkingPaperRepository _papers;

        public AnalyzeEvidenceCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity, IRiskRepository risks,
            IProcedureRepository procedures, IEvidenceRepository evidence,
            IWorkingPaperRepository papers)
            : base(ai, access, engagements, clients, activity) {
            _risks = risks;
            _procedures = procedures;
            _evidence = evidence;
            _papers = papers;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AnalyzeEvidenceCommand request,
            CancellationToken ct) {
            await Access.EnsureMemberAsync(request.EngagementId, ct);

            var overview = EngagementAiContext.OverviewAsync(request.EngagementId,
                Engagements, Clients, ct);
            var risks = EngagementAiContext.RisksAsync(request.EngagementId, _risks, ct);
            var procedures = EngagementAiContext.ProceduresAsync(request.EngagementId,
                _procedures, ct);
            var evidence = EngagementAiContext.EvidenceAsync(request.EngagementId, _evidence, ct);
            var papers = EngagementAiContext.WorkingPapersAsync(request.EngagementId, _papers, ct);

            await Task.WhenAll(overview, risks, procedures, evidence, papers);

            var context = new List<AiDocument>(overview.Result);

            foreach (var document in new[] {
                risks.Result, procedures.Result, evidence.Result, papers.Result
            }) {
                if (document is not null) {
                    context.Add(document);
                }
            }

            return await RunAsync(request.EngagementId, AuditAiCapabilities.EvidenceAnalysis,
                "Assess the evidence held on this engagement: which procedures and risks are " +
                "supported by evidence, which are not, where the evidence looks thin for the " +
                "significance of what it supports, and which evidence items are attached to " +
                "nothing. Base every statement on the evidence register as given.",
                "Where are the evidence gaps on this engagement?",
                context, "ai.evidence_analysis", "generated an AI evidence-gap analysis.", ct);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AnalyzeEngagementCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string? Question { get; set; }
    }

    public class AnalyzeEngagementCommandValidator
        : AbstractValidator<AnalyzeEngagementCommand> {
        public AnalyzeEngagementCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Question).MaximumLength(2000);
        }
    }

    /// <summary>
    /// Engagement intelligence: the whole record in one pass, so the answer can connect a risk
    /// to the procedure that responds to it, the evidence behind it and the finding it produced,
    /// instead of treating each document on its own.
    /// </summary>
    public class AnalyzeEngagementCommandHandler
        : IRequestHandler<AnalyzeEngagementCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly EngagementReadSet _reads;
        private readonly IActivityRecorder _activity;

        public AnalyzeEngagementCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _reads = reads;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AnalyzeEngagementCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.FullAsync(request.EngagementId, _reads, ct);

            if (context.Count == 0) {
                return Result<AiProposalResult>.Error("The engagement was not found.");
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.EngagementIntelligence,
                "Answer using the engagement as a whole. Connect risks to the procedures that " +
                "respond to them, the working papers and evidence behind those procedures, " +
                "and the findings they produced. Point out where those connections are " +
                "missing. Distinguish what the record states from what you infer from it.",
                request.Question ?? "What does the engagement record show as a whole?",
                context, request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.engagement_intelligence",
                "Engagement", request.EngagementId,
                "generated an AI engagement-wide analysis.", request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.EngagementIntelligence, completion));
        }
    }
}
