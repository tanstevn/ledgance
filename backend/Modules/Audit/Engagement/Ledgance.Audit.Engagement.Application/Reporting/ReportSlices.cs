using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Reporting {
    [RequiresPermission(AuditEngagementPermissions.Manage)]
    public class SaveAuditReportCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public AuditOpinion Opinion { get; set; }
        public string BasisForOpinion { get; set; } = string.Empty;
        public string KeyAuditMatters { get; set; } = string.Empty;
        public string OtherInformation { get; set; } = string.Empty;
    }

    public class SaveAuditReportCommandValidator : AbstractValidator<SaveAuditReportCommand> {
        public SaveAuditReportCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Opinion).IsInEnum();
        }
    }

    public class SaveAuditReportCommandHandler
        : IRequestHandler<SaveAuditReportCommand, Result<Guid>> {
        private readonly IReportRepository _reports;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public SaveAuditReportCommandHandler(IReportRepository reports,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _reports = reports;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(SaveAuditReportCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindByEngagementAsync(request.EngagementId, ct)
                ?? AuditReport.Draft(request.EngagementId);

            report.UpdateDraft(request.Opinion, request.BasisForOpinion,
                request.KeyAuditMatters, request.OtherInformation);

            await _reports.UpsertAsync(report, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "report.drafted",
                "AuditReport", report.Id, "The audit report draft was updated.",
                request.EngagementId), ct);

            return Result<Guid>.Success(report.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Approve)]
    public class FinalizeAuditReportCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
    }

    public class FinalizeAuditReportCommandValidator
        : AbstractValidator<FinalizeAuditReportCommand> {
        public FinalizeAuditReportCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class FinalizeAuditReportCommandHandler
        : IRequestHandler<FinalizeAuditReportCommand, Result<bool>> {
        private readonly IReportRepository _reports;
        private readonly IFindingRepository _findings;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public FinalizeAuditReportCommandHandler(IReportRepository reports,
            IFindingRepository findings, IEngagementAccessGuard access,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _reports = reports;
            _findings = findings;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(FinalizeAuditReportCommand request,
            CancellationToken ct) {
            var access = await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindByEngagementAsync(request.EngagementId, ct);

            if (report is null) {
                return Result<bool>.Error("There is no audit report draft to finalize.");
            }

            var findings = await _findings.ListAsync(request.EngagementId, ct);
            var openFindings = findings.Count(finding => finding.IsOpen);

            report.Finalize(_currentUser.Require().UserId, access.TeamRole, openFindings);
            await _reports.UpsertAsync(report, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "report.finalized",
                "AuditReport", report.Id, "The audit report was finalized.",
                request.EngagementId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetAuditReportQuery : IQuery<Result<AuditReportView>> {
        public Guid EngagementId { get; set; }
    }

    public class AuditReportView {
        public Guid Id { get; set; }
        public string Opinion { get; set; } = string.Empty;
        public string BasisForOpinion { get; set; } = string.Empty;
        public string KeyAuditMatters { get; set; } = string.Empty;
        public string OtherInformation { get; set; } = string.Empty;
        public bool IsFinalized { get; set; }
        public Guid? FinalizedBy { get; set; }
        public DateTime? FinalizedAt { get; set; }
    }

    public class GetAuditReportQueryHandler
        : IRequestHandler<GetAuditReportQuery, Result<AuditReportView>> {
        private readonly IReportRepository _reports;
        private readonly IEngagementAccessGuard _access;

        public GetAuditReportQueryHandler(IReportRepository reports,
            IEngagementAccessGuard access) {
            _reports = reports;
            _access = access;
        }

        public async Task<Result<AuditReportView>> HandleAsync(GetAuditReportQuery request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var report = await _reports.FindByEngagementAsync(request.EngagementId, ct);

            if (report is null) {
                return Result<AuditReportView>.Error("No audit report has been drafted yet.");
            }

            return Result<AuditReportView>.Success(new AuditReportView {
                Id = report.Id,
                Opinion = report.Opinion.ToString(),
                BasisForOpinion = report.BasisForOpinion,
                KeyAuditMatters = report.KeyAuditMatters,
                OtherInformation = report.OtherInformation,
                IsFinalized = report.IsFinalized,
                FinalizedBy = report.FinalizedBy,
                FinalizedAt = report.FinalizedAt
            });
        }
    }
}
