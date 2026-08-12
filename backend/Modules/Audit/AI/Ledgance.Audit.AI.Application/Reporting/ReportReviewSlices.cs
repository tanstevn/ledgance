using FluentValidation;
using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.AI.Application.Reporting {
    /// <summary>
    /// Who may sign off on an AI draft. Engagement managers and partners can, because they are
    /// the ones who review work on the engagement; organization Admins and Owners reach the
    /// engagement through oversight, which is read access, not review authority.
    /// </summary>
    internal static class ReportReviewAuthority {
        public static bool Held(EngagementAccess access) =>
            access.TeamRole is EngagementRole.Manager or EngagementRole.Partner;
    }

    [RequiresPermission(AuditEngagementPermissions.Approve)]
    public class ReviewGeneratedReportCommand : ICommand<Result<GeneratedReportView>> {
        public Guid EngagementId { get; set; }
        public Guid ReportId { get; set; }
        public bool Accept { get; set; }
        public string? Note { get; set; }
    }

    public class ReviewGeneratedReportCommandValidator
        : AbstractValidator<ReviewGeneratedReportCommand> {
        public ReviewGeneratedReportCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.ReportId).NotEmpty();
            RuleFor(x => x.Note).MaximumLength(2000);
        }
    }

    /// <summary>
    /// The only way an AI draft stops being unreviewed. Accepting it records who took
    /// professional responsibility for working from it — it does not write the audit report and
    /// it does not finalize anything; the engagement partner still finalizes the report itself.
    /// </summary>
    public class ReviewGeneratedReportCommandHandler
        : IRequestHandler<ReviewGeneratedReportCommand, Result<GeneratedReportView>> {
        private readonly IGeneratedReportRepository _reports;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ReviewGeneratedReportCommandHandler(IGeneratedReportRepository reports,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _reports = reports;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<GeneratedReportView>> HandleAsync(
            ReviewGeneratedReportCommand request, CancellationToken ct) {
            var access = await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindAsync(request.ReportId, ct);

            if (report is null || report.EngagementId != request.EngagementId) {
                return Result<GeneratedReportView>.Error("That generated report was not found.");
            }

            var reviewer = _currentUser.Require().UserId;
            var authority = ReportReviewAuthority.Held(access);

            if (request.Accept) {
                report.Accept(reviewer, authority, request.Note);
            }
            else {
                report.Reject(reviewer, authority, request.Note ?? string.Empty);
            }

            await _reports.UpdateAsync(report, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit",
                request.Accept ? "ai.report_accepted" : "ai.report_rejected",
                "GeneratedAuditReport", report.Id,
                request.Accept
                    ? $"reviewed and accepted the AI draft \"{report.Title}\" as a working basis."
                    : $"reviewed and rejected the AI draft \"{report.Title}\".",
                request.EngagementId), ct);

            return Result<GeneratedReportView>.Success(GeneratedReportView.From(report));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetGeneratedReportsQuery : IQuery<Result<IEnumerable<GeneratedReportView>>> {
        public Guid EngagementId { get; set; }
    }

    public class GetGeneratedReportsQueryHandler
        : IRequestHandler<GetGeneratedReportsQuery, Result<IEnumerable<GeneratedReportView>>> {
        private readonly IGeneratedReportRepository _reports;
        private readonly IEngagementAccessGuard _access;

        public GetGeneratedReportsQueryHandler(IGeneratedReportRepository reports,
            IEngagementAccessGuard access) {
            _reports = reports;
            _access = access;
        }

        public async Task<Result<IEnumerable<GeneratedReportView>>> HandleAsync(
            GetGeneratedReportsQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var reports = await _reports.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<GeneratedReportView>>.Success(
                reports.Select(GeneratedReportView.From));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetGeneratedReportByIdQuery : IQuery<Result<GeneratedReportView>> {
        public Guid EngagementId { get; set; }
        public Guid ReportId { get; set; }
    }

    public class GetGeneratedReportByIdQueryHandler
        : IRequestHandler<GetGeneratedReportByIdQuery, Result<GeneratedReportView>> {
        private readonly IGeneratedReportRepository _reports;
        private readonly IEngagementAccessGuard _access;

        public GetGeneratedReportByIdQueryHandler(IGeneratedReportRepository reports,
            IEngagementAccessGuard access) {
            _reports = reports;
            _access = access;
        }

        public async Task<Result<GeneratedReportView>> HandleAsync(
            GetGeneratedReportByIdQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindAsync(request.ReportId, ct);

            return report is null || report.EngagementId != request.EngagementId
                ? Result<GeneratedReportView>.Error("That generated report was not found.")
                : Result<GeneratedReportView>.Success(GeneratedReportView.From(report));
        }
    }
}
