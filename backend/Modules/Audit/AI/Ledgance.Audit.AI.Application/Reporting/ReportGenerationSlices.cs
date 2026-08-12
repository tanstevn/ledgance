using FluentValidation;
using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Application.Reporting {
    /// <summary>
    /// Shared plumbing for the report generators: assemble the engagement record the caller may
    /// read, ask the model for structured sections, and persist the answer as a draft awaiting
    /// review. Nothing here writes to the audit report itself.
    /// </summary>
    public abstract class ReportGenerationHandlerBase {
        protected readonly IAiCompletionService Ai;
        protected readonly IEngagementAccessGuard Access;
        protected readonly EngagementReadSet Reads;
        protected readonly IGeneratedReportRepository Reports;
        protected readonly ICurrentUserAccessor CurrentUser;
        protected readonly IActivityRecorder Activity;

        protected ReportGenerationHandlerBase(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IGeneratedReportRepository reports, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            Ai = ai;
            Access = access;
            Reads = reads;
            Reports = reports;
            CurrentUser = currentUser;
            Activity = activity;
        }

        protected async Task<Result<GeneratedReportView>> GenerateAsync(Guid engagementId,
            AuditAiCapability capability, IReadOnlyList<AuditReportSection> sections,
            string title, string instruction, string userPrompt, CancellationToken ct) {
            await Access.EnsureMemberAsync(engagementId, ct);

            var context = await EngagementAiContext.FullAsync(engagementId, Reads, ct);

            if (context.Count == 0) {
                return Result<GeneratedReportView>.Error("The engagement was not found.");
            }

            var completion = await Ai.CompleteAsync(AuditAiPrompts.ReportWorkload(capability,
                $"{instruction}\n\n{ReportComposition.FormatInstruction(sections)}",
                userPrompt, context, engagementId), ct);

            var report = GeneratedAuditReport.Draft(engagementId, capability.Key,
                capability.RequiredReportScope, title,
                ReportComposition.Parse(completion.Content, sections),
                completion.Provider, completion.Model, CurrentUser.Require().UserId);

            await Reports.AddAsync(report, ct);

            await Activity.RecordAsync(new ActivityEntry("Audit", "ai.report_generated",
                "GeneratedAuditReport", report.Id,
                $"generated an AI draft report ({report.Sections.Count} sections) using " +
                $"{completion.Provider}/{completion.Model}; it is awaiting professional review.",
                engagementId), ct);

            return Result<GeneratedReportView>.Success(GeneratedReportView.From(report));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class GenerateReportSectionCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public AuditReportSection Section { get; set; }
        public string? Instruction { get; set; }
    }

    public class GenerateReportSectionCommandValidator
        : AbstractValidator<GenerateReportSectionCommand> {
        public GenerateReportSectionCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Section).IsInEnum();
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    /// <summary>
    /// The entry level of AI report writing: one section at a time, returned as an editable
    /// proposal rather than stored as a report, because a section is a drafting aid.
    /// </summary>
    public class GenerateReportSectionCommandHandler
        : IRequestHandler<GenerateReportSectionCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly EngagementReadSet _reads;
        private readonly IActivityRecorder _activity;

        public GenerateReportSectionCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _reads = reads;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            GenerateReportSectionCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.FullAsync(request.EngagementId, _reads, ct);

            if (context.Count == 0) {
                return Result<AiProposalResult>.Error("The engagement was not found.");
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.ReportWorkload(
                AuditAiCapabilities.ReportSection,
                $"Draft the '{request.Section}' section of this engagement's audit report. " +
                "Write the section only — no other sections, no preamble. End with a short " +
                "'Sources' line naming the engagement records the section rests on.",
                request.Instruction is null
                    ? $"Draft the {request.Section} section."
                    : $"Draft the {request.Section} section. {request.Instruction}",
                context, request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.report_section",
                "Engagement", request.EngagementId,
                $"generated an AI draft of the {request.Section} report section.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.ReportSection, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class GenerateDraftReportCommand : ICommand<Result<GeneratedReportView>> {
        public Guid EngagementId { get; set; }
        public string? Instruction { get; set; }
    }

    public class GenerateDraftReportCommandValidator
        : AbstractValidator<GenerateDraftReportCommand> {
        public GenerateDraftReportCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    public class GenerateDraftReportCommandHandler : ReportGenerationHandlerBase,
        IRequestHandler<GenerateDraftReportCommand, Result<GeneratedReportView>> {
        public GenerateDraftReportCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IGeneratedReportRepository reports, ICurrentUserAccessor currentUser,
            IActivityRecorder activity)
            : base(ai, access, reads, reports, currentUser, activity) { }

        public Task<Result<GeneratedReportView>> HandleAsync(GenerateDraftReportCommand request,
            CancellationToken ct) =>
            GenerateAsync(request.EngagementId, AuditAiCapabilities.ReportDraft,
                ReportSectionSets.FullDraft, "Draft audit report",
                "Draft the complete audit report for this engagement from the engagement " +
                "record: the risks identified, the procedures performed, the evidence " +
                "obtained and the findings raised. Keep the sections consistent with one " +
                "another — a finding described in one section must be described the same way " +
                "in every other.",
                request.Instruction ?? "Draft the audit report for this engagement.", ct);
    }

    [RequiresPermission(AuditEngagementPermissions.Manage)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class GenerateEngagementReportCommand : ICommand<Result<GeneratedReportView>> {
        public Guid EngagementId { get; set; }

        /// <summary>
        /// Who the draft is written for. A reviewer draft surfaces open questions and gaps; a
        /// management draft states outcomes and what management should do about them.
        /// </summary>
        public bool ForReviewer { get; set; }

        public string? Instruction { get; set; }
    }

    public class GenerateEngagementReportCommandValidator
        : AbstractValidator<GenerateEngagementReportCommand> {
        public GenerateEngagementReportCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    public class GenerateEngagementReportCommandHandler : ReportGenerationHandlerBase,
        IRequestHandler<GenerateEngagementReportCommand, Result<GeneratedReportView>> {
        public GenerateEngagementReportCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IGeneratedReportRepository reports, ICurrentUserAccessor currentUser,
            IActivityRecorder activity)
            : base(ai, access, reads, reports, currentUser, activity) { }

        public Task<Result<GeneratedReportView>> HandleAsync(
            GenerateEngagementReportCommand request, CancellationToken ct) =>
            GenerateAsync(request.EngagementId, AuditAiCapabilities.EngagementReport,
                ReportSectionSets.Engagement,
                request.ForReviewer ? "Engagement report (reviewer draft)"
                    : "Engagement report (management draft)",
                request.ForReviewer
                    ? "Produce a reviewer-oriented engagement report. Alongside the report " +
                      "content, call out every place the engagement record is incomplete, " +
                      "contradictory or unresolved, so the reviewer can act on it before " +
                      "sign-off."
                    : "Produce a management-oriented engagement report. State what was done, " +
                      "what was found and what management should act on, in language a " +
                      "non-auditor can follow, without softening any finding.",
                request.Instruction ?? "Produce the engagement report.", ct);
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class RegenerateReportSectionCommand : ICommand<Result<GeneratedReportView>> {
        public Guid EngagementId { get; set; }
        public Guid ReportId { get; set; }
        public AuditReportSection Section { get; set; }
        public string? Instruction { get; set; }
    }

    public class RegenerateReportSectionCommandValidator
        : AbstractValidator<RegenerateReportSectionCommand> {
        public RegenerateReportSectionCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.ReportId).NotEmpty();
            RuleFor(x => x.Section).IsInEnum();
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    /// <summary>
    /// Rewrites one section of an existing draft and stores the result as a new draft, so the
    /// version a reviewer looked at is never edited underneath them.
    /// </summary>
    public class RegenerateReportSectionCommandHandler
        : IRequestHandler<RegenerateReportSectionCommand, Result<GeneratedReportView>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly EngagementReadSet _reads;
        private readonly IGeneratedReportRepository _reports;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public RegenerateReportSectionCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IGeneratedReportRepository reports, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _reads = reads;
            _reports = reports;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<GeneratedReportView>> HandleAsync(
            RegenerateReportSectionCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var existing = await _reports.FindAsync(request.ReportId, ct);

            if (existing is null || existing.EngagementId != request.EngagementId) {
                return Result<GeneratedReportView>.Error("That generated report was not found.");
            }

            var context = await EngagementAiContext.FullAsync(request.EngagementId, _reads, ct);

            var completion = await _ai.CompleteAsync(AuditAiPrompts.ReportWorkload(
                AuditAiCapabilities.ReportDraft,
                $"Rewrite the '{request.Section}' section of the audit report so it is " +
                "consistent with the rest of the draft and with the engagement record.\n\n" +
                ReportComposition.FormatInstruction([request.Section]),
                request.Instruction is null
                    ? $"Rewrite the {request.Section} section. The current text is:\n" +
                      SectionText(existing, request.Section)
                    : $"Rewrite the {request.Section} section. {request.Instruction}\n" +
                      $"The current text is:\n{SectionText(existing, request.Section)}",
                context, request.EngagementId), ct);

            var replacement = ReportComposition.Parse(completion.Content, [request.Section])[0];

            var sections = existing.Sections
                .Select(section => section.Section == request.Section ? replacement : section)
                .ToList();

            if (sections.All(section => section.Section != request.Section)) {
                sections.Add(replacement);
            }

            var regenerated = GeneratedAuditReport.Draft(existing.EngagementId,
                existing.Capability, existing.ReportScope, existing.Title, sections,
                completion.Provider, completion.Model, _currentUser.Require().UserId);

            await _reports.AddAsync(regenerated, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.report_regenerated",
                "GeneratedAuditReport", regenerated.Id,
                $"regenerated the {request.Section} section of an AI draft report.",
                request.EngagementId), ct);

            return Result<GeneratedReportView>.Success(
                GeneratedReportView.From(regenerated));
        }

        private static string SectionText(GeneratedAuditReport report,
            AuditReportSection section) =>
            report.Sections.FirstOrDefault(candidate => candidate.Section == section)?.Content
                ?? "(the draft has no such section yet)";
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class CheckReportConsistencyCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public Guid ReportId { get; set; }
    }

    public class CheckReportConsistencyCommandValidator
        : AbstractValidator<CheckReportConsistencyCommand> {
        public CheckReportConsistencyCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.ReportId).NotEmpty();
        }
    }

    /// <summary>
    /// Reads a stored draft back against the engagement record and reports where the two
    /// disagree. This is the check that makes an AI draft safe to work from: it looks for
    /// statements the record does not support, not for style.
    /// </summary>
    public class CheckReportConsistencyCommandHandler
        : IRequestHandler<CheckReportConsistencyCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly EngagementReadSet _reads;
        private readonly IGeneratedReportRepository _reports;
        private readonly IActivityRecorder _activity;

        public CheckReportConsistencyCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, EngagementReadSet reads,
            IGeneratedReportRepository reports, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _reads = reads;
            _reports = reports;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            CheckReportConsistencyCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindAsync(request.ReportId, ct);

            if (report is null || report.EngagementId != request.EngagementId) {
                return Result<AiProposalResult>.Error("That generated report was not found.");
            }

            var context = await EngagementAiContext.FullAsync(request.EngagementId, _reads, ct);

            context.Add(new AiDocument("Report draft under review",
                string.Join("\n\n", report.Sections.Select(section =>
                    $"## {section.Heading} ({section.Section})\n{section.Content}"))));

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.ReportConsistency,
                "Check the report draft against the engagement record. Report, in priority " +
                "order: statements the record does not support, numbers that disagree with " +
                "the record, findings or risks the record contains but the draft omits, and " +
                "sections that contradict one another. Quote the wording at issue. If a " +
                "section is consistent, say so briefly rather than inventing a concern.",
                "Check this draft for inconsistencies with the engagement record.",
                context, request.EngagementId), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.report_consistency",
                "GeneratedAuditReport", report.Id,
                "ran an AI consistency check over a draft report.", request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.ReportConsistency, completion));
        }
    }
}
