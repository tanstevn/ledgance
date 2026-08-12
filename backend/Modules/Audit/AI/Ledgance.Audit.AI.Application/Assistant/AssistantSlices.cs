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

namespace Ledgance.Audit.AI.Application.Assistant {
    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class AskAuditAssistantCommand : ICommand<Result<AiProposalResult>> {
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// When set, the assistant answers in the context of this engagement; the caller must be
        /// on its team. When null, it answers general audit methodology questions only.
        /// </summary>
        public Guid? EngagementId { get; set; }
    }

    public class AskAuditAssistantCommandValidator : AbstractValidator<AskAuditAssistantCommand> {
        public AskAuditAssistantCommandValidator() {
            RuleFor(x => x.Question).NotEmpty().MaximumLength(4000);
        }
    }

    public class AskAuditAssistantCommandHandler
        : IRequestHandler<AskAuditAssistantCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IRiskRepository _risks;
        private readonly IActivityRecorder _activity;

        public AskAuditAssistantCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IRiskRepository risks, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _risks = risks;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            AskAuditAssistantCommand request, CancellationToken ct) {
            var context = new List<AiDocument>();

            if (request.EngagementId is not null) {
                await _access.EnsureMemberAsync(request.EngagementId.Value, ct);

                context.AddRange(await EngagementAiContext.OverviewAsync(
                    request.EngagementId.Value, _engagements, _clients, ct));

                if (await EngagementAiContext.RisksAsync(request.EngagementId.Value,
                        _risks, ct) is { } risks) {
                    context.Add(risks);
                }
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.Assistant,
                "Answer the auditor's question. Use the engagement context when provided; " +
                "otherwise answer from general audit methodology and say that no engagement " +
                "context was available.",
                request.Question, context, request.EngagementId), ct);

            if (request.EngagementId is not null) {
                await _activity.RecordAsync(new ActivityEntry("Audit", "ai.assistant",
                    "Engagement", request.EngagementId.Value,
                    "asked the AI assistant about this engagement.",
                    request.EngagementId), ct);
            }

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.Assistant, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class SummarizeDocumentCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public Guid? WorkingPaperId { get; set; }
        public Guid? EvidenceId { get; set; }
    }

    public class SummarizeDocumentCommandValidator : AbstractValidator<SummarizeDocumentCommand> {
        public SummarizeDocumentCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x)
                .Must(x => x.WorkingPaperId is not null || x.EvidenceId is not null)
                .WithMessage("Provide a working paper or an evidence item to summarize.");
        }
    }

    public class SummarizeDocumentCommandHandler
        : IRequestHandler<SummarizeDocumentCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IWorkingPaperRepository _papers;
        private readonly IEvidenceRepository _evidence;
        private readonly IActivityRecorder _activity;

        public SummarizeDocumentCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IWorkingPaperRepository papers,
            IEvidenceRepository evidence, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _papers = papers;
            _evidence = evidence;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            SummarizeDocumentCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = new List<AiDocument>();
            Guid subjectId;
            string subjectType;

            if (request.WorkingPaperId is not null) {
                var paper = await _papers.FindAsync(request.WorkingPaperId.Value, ct);

                if (paper is null || paper.EngagementId != request.EngagementId) {
                    return Result<AiProposalResult>.Error("Working paper was not found.");
                }

                context.Add(new AiDocument($"Working paper {paper.Reference}: {paper.Title}",
                    paper.Content));
                subjectId = paper.Id;
                subjectType = "WorkingPaper";
            }
            else {
                var item = await _evidence.FindAsync(request.EvidenceId!.Value, ct);

                if (item is null || item.EngagementId != request.EngagementId) {
                    return Result<AiProposalResult>.Error("Evidence was not found.");
                }

                // Evidence file content is binary; the summary works from its recorded metadata
                // and description. Content-level analysis arrives with document extraction.
                context.Add(new AiDocument($"Evidence: {item.FileName}",
                    $"File name: {item.FileName}\nContent type: {item.ContentType}\n" +
                    $"Size: {item.SizeBytes} bytes\nVersion: {item.Version}\n" +
                    $"Description: {item.Description}"));
                subjectId = item.Id;
                subjectType = "Evidence";
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.DocumentSummary,
                "Summarize the document for an audit reviewer: purpose, key contents, " +
                "conclusions reached, and anything that looks unresolved or inconsistent.",
                "Summarize the attached document.", context, request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.document_summary",
                subjectType, subjectId, "generated an AI summary.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.DocumentSummary, completion));
        }
    }

    public class GetAuditAiCapabilitiesQuery
        : IQuery<Result<IEnumerable<AuditAiCapabilityRow>>> { }

    public class AuditAiCapabilityRow {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RequiredTier { get; set; } = string.Empty;
        public string RequiredReportScope { get; set; } = string.Empty;
        public string RequiredAnalysisScope { get; set; } = string.Empty;

        /// <summary>
        /// The cheapest plan that includes this capability, resolved from the catalogue so the
        /// UI can name the upgrade without holding plan rules of its own.
        /// </summary>
        public string RequiredPlan { get; set; } = string.Empty;

        public bool Included { get; set; }
    }

    public class GetAuditAiCapabilitiesQueryHandler
        : IRequestHandler<GetAuditAiCapabilitiesQuery, Result<IEnumerable<AuditAiCapabilityRow>>> {
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;

        public GetAuditAiCapabilitiesQueryHandler(IEntitlementService entitlements,
            ICurrentUserAccessor currentUser) {
            _entitlements = entitlements;
            _currentUser = currentUser;
        }

        public async Task<Result<IEnumerable<AuditAiCapabilityRow>>> HandleAsync(
            GetAuditAiCapabilitiesQuery request, CancellationToken ct) {
            var entitlements = await _entitlements.GetAsync(
                _currentUser.RequireOrganizationId(), ProductModule.Audit, ct);

            var aiEnabled = entitlements.Has(Entitlements.AiEnabled);
            var permittedTier = entitlements.Tier(Entitlements.AiMaxTier);
            var permittedReports = entitlements.Value(Entitlements.AiReportScope,
                AiReportScopes.None);
            var permittedAnalysis = entitlements.Value(Entitlements.AiAnalysisScope,
                AiAnalysisScopes.Document);

            return Result<IEnumerable<AuditAiCapabilityRow>>.Success(AuditAiCapabilities.All
                .Select(capability => new AuditAiCapabilityRow {
                    Key = capability.Key,
                    Description = capability.Description,
                    RequiredTier = capability.RequiredTier,
                    RequiredReportScope = capability.RequiredReportScope,
                    RequiredAnalysisScope = capability.RequiredAnalysisScope,
                    RequiredPlan = CheapestPlanIncluding(capability),
                    Included = aiEnabled
                        && AiTiers.Allows(permittedTier, capability.RequiredTier)
                        && AiReportScopes.Allows(permittedReports,
                            capability.RequiredReportScope)
                        && AiAnalysisScopes.Allows(permittedAnalysis,
                            capability.RequiredAnalysisScope)
                }));
        }

        private static string CheapestPlanIncluding(AuditAiCapability capability) =>
            SubscriptionPlanCatalog.Ordered(ProductModule.Audit)
                .FirstOrDefault(plan => Grants(SubscriptionPlanCatalog.For(plan), capability))
                .ToString();

        private static bool Grants(IReadOnlyDictionary<string, string> values,
            AuditAiCapability capability) =>
            AiTiers.Allows(values[Entitlements.AiMaxTier], capability.RequiredTier)
            && AiReportScopes.Allows(values[Entitlements.AiReportScope],
                capability.RequiredReportScope)
            && AiAnalysisScopes.Allows(values[Entitlements.AiAnalysisScope],
                capability.RequiredAnalysisScope);
    }
}
