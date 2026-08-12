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

namespace Ledgance.Audit.AI.Application.Assistant {
    /// <summary>
    /// The summarizing and note-taking capabilities every plan includes, Free among them. They
    /// restate what the engagement record already says, which is why they need no report
    /// entitlement: nothing here drafts a report.
    /// </summary>
    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class SummarizeFindingsCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }

        /// <summary>Summarizes one finding when set, the whole finding register when not.</summary>
        public Guid? FindingId { get; set; }
    }

    public class SummarizeFindingsCommandValidator
        : AbstractValidator<SummarizeFindingsCommand> {
        public SummarizeFindingsCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class SummarizeFindingsCommandHandler
        : IRequestHandler<SummarizeFindingsCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IFindingRepository _findings;
        private readonly IActivityRecorder _activity;

        public SummarizeFindingsCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IFindingRepository findings,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _findings = findings;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            SummarizeFindingsCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = new List<AiDocument>();

            if (request.FindingId is not null) {
                var finding = await _findings.FindAsync(request.FindingId.Value, ct);

                if (finding is null || finding.EngagementId != request.EngagementId) {
                    return Result<AiProposalResult>.Error("The finding was not found.");
                }

                context.Add(new AiDocument($"Finding: {finding.Title}",
                    $"Severity: {finding.Severity}\nStatus: {finding.Status}\n" +
                    $"Description: {finding.Description}\n" +
                    $"Recommendation: {finding.Recommendation}\n" +
                    $"Resolution: {finding.Resolution ?? "not resolved"}"));
            }
            else if (await EngagementAiContext.FindingsAsync(request.EngagementId, _findings, ct)
                     is { } findings) {
                context.Add(findings);
            }
            else {
                return Result<AiProposalResult>.Error(
                    "This engagement has no findings to summarize yet.");
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.FindingSummary,
                "Summarize the finding or findings in plain language for someone who has not " +
                "read the file: what was found, why it matters, and what is being recommended. " +
                "Keep the severity as recorded — do not re-rate it.",
                "Summarize these findings.", context, request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.finding_summary",
                "Engagement", request.EngagementId, "generated an AI finding summary.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.FindingSummary, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class SummarizeEngagementCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class SummarizeEngagementCommandValidator
        : AbstractValidator<SummarizeEngagementCommand> {
        public SummarizeEngagementCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class SummarizeEngagementCommandHandler
        : IRequestHandler<SummarizeEngagementCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IFindingRepository _findings;
        private readonly IActivityRecorder _activity;

        public SummarizeEngagementCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IFindingRepository findings, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _findings = findings;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            SummarizeEngagementCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            if (context.Count == 0) {
                return Result<AiProposalResult>.Error("The engagement was not found.");
            }

            if (await EngagementAiContext.FindingsAsync(request.EngagementId, _findings, ct)
                is { } findings) {
                context.Add(findings);
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.EngagementSummary,
                "Write a short status summary of this engagement — no more than a few " +
                "paragraphs: where it stands, what has been found so far, and what is still " +
                "open. State plainly where the record is thin rather than inferring progress.",
                "Summarize where this engagement stands.", context,
                request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.engagement_summary",
                "Engagement", request.EngagementId, "generated an AI engagement summary.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.EngagementSummary, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class DraftEngagementNoteCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string Observation { get; set; } = string.Empty;
    }

    public class DraftEngagementNoteCommandValidator
        : AbstractValidator<DraftEngagementNoteCommand> {
        public DraftEngagementNoteCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Observation).NotEmpty().MaximumLength(4000);
        }
    }

    public class DraftEngagementNoteCommandHandler
        : IRequestHandler<DraftEngagementNoteCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IActivityRecorder _activity;

        public DraftEngagementNoteCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            DraftEngagementNoteCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.NoteDraft,
                "Turn the auditor's rough observation into a clean engagement note: what was " +
                "observed, when and where it applies, and what follow-up it implies. Add " +
                "nothing the observation does not contain.",
                $"Write up this observation as a note: {request.Observation}", context,
                request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.note_draft",
                "Engagement", request.EngagementId, "generated an AI engagement note.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.NoteDraft, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class ImproveWorkingPaperWordingCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class ImproveWorkingPaperWordingCommandValidator
        : AbstractValidator<ImproveWorkingPaperWordingCommand> {
        public ImproveWorkingPaperWordingCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Text).NotEmpty().MaximumLength(8000);
        }
    }

    /// <summary>
    /// Wording help rather than drafting: it may only rewrite the text it is given, which is
    /// what makes it safe to include on Free while structured working-paper drafting is not.
    /// </summary>
    public class ImproveWorkingPaperWordingCommandHandler
        : IRequestHandler<ImproveWorkingPaperWordingCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public ImproveWorkingPaperWordingCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            ImproveWorkingPaperWordingCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.WordingAssistance,
                "Rewrite the auditor's text so it reads as clear, neutral working-paper " +
                "prose. Keep every fact, figure and conclusion exactly as written — do not " +
                "add work, evidence or judgments that are not already in the text, and do not " +
                "soften or strengthen any conclusion.",
                $"Rewrite this working-paper text:\n\n{request.Text}", null,
                request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.wording_assistance",
                "Engagement", request.EngagementId,
                "used AI wording assistance on working-paper text.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.WordingAssistance, completion));
        }
    }
}
