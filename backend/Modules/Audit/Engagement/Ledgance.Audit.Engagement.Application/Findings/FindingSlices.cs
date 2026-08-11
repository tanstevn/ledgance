using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Findings {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class RaiseFindingCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FindingSeverity Severity { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public List<Guid> EvidenceIds { get; set; } = [];
    }

    public class RaiseFindingCommandValidator : AbstractValidator<RaiseFindingCommand> {
        public RaiseFindingCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).NotEmpty();
            RuleFor(x => x.Severity).IsInEnum();
        }
    }

    public class RaiseFindingCommandHandler : IRequestHandler<RaiseFindingCommand, Result<Guid>> {
        private readonly IFindingRepository _findings;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public RaiseFindingCommandHandler(IFindingRepository findings,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _findings = findings;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(RaiseFindingCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var finding = Finding.Raise(request.EngagementId, request.Title,
                request.Description, request.Severity, request.Recommendation,
                request.EvidenceIds, _currentUser.Require().UserId);

            await _findings.AddAsync(finding, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "finding.raised",
                "Finding", finding.Id,
                $"raised the {finding.Severity} finding {finding.Title}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(finding.Id);
        }
    }

    public enum FindingAction { Resolve, AcceptRisk, Close }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class UpdateFindingStatusCommand : ICommand<Result<string>> {
        public Guid EngagementId { get; set; }
        public Guid FindingId { get; set; }
        public FindingAction Action { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateFindingStatusCommandValidator
        : AbstractValidator<UpdateFindingStatusCommand> {
        public UpdateFindingStatusCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.FindingId).NotEmpty();
            RuleFor(x => x.Action).IsInEnum();
        }
    }

    public class UpdateFindingStatusCommandHandler
        : IRequestHandler<UpdateFindingStatusCommand, Result<string>> {
        private readonly IFindingRepository _findings;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public UpdateFindingStatusCommandHandler(IFindingRepository findings,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _findings = findings;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<string>> HandleAsync(UpdateFindingStatusCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var finding = await _findings.FindAsync(request.FindingId, ct);

            if (finding is null || finding.EngagementId != request.EngagementId) {
                return Result<string>.Error("Finding was not found.");
            }

            switch (request.Action) {
                case FindingAction.Resolve:
                    finding.Resolve(request.Note ?? string.Empty);
                    break;
                case FindingAction.AcceptRisk:
                    finding.AcceptRisk(request.Note ?? string.Empty);
                    break;
                case FindingAction.Close:
                    finding.Close();
                    break;
            }

            await _findings.UpdateAsync(finding, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "finding.status_changed",
                "Finding", finding.Id,
                $"marked the finding {finding.Title} as {finding.Status}.",
                request.EngagementId), ct);

            return Result<string>.Success(finding.Status.ToString());
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetFindingsQuery : IQuery<Result<IEnumerable<FindingRow>>> {
        public Guid EngagementId { get; set; }
    }

    public class FindingRow {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string? Resolution { get; set; }
        public List<Guid> EvidenceIds { get; set; } = [];
        public Guid RaisedBy { get; set; }
        public DateTime RaisedAt { get; set; }
    }

    public class GetFindingsQueryHandler
        : IRequestHandler<GetFindingsQuery, Result<IEnumerable<FindingRow>>> {
        private readonly IFindingRepository _findings;
        private readonly IEngagementAccessGuard _access;

        public GetFindingsQueryHandler(IFindingRepository findings,
            IEngagementAccessGuard access) {
            _findings = findings;
            _access = access;
        }

        public async Task<Result<IEnumerable<FindingRow>>> HandleAsync(
            GetFindingsQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var findings = await _findings.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<FindingRow>>.Success(findings
                .Select(finding => new FindingRow {
                    Id = finding.Id,
                    Title = finding.Title,
                    Description = finding.Description,
                    Severity = finding.Severity.ToString(),
                    Status = finding.Status.ToString(),
                    Recommendation = finding.Recommendation,
                    Resolution = finding.Resolution,
                    EvidenceIds = finding.EvidenceIds,
                    RaisedBy = finding.RaisedBy,
                    RaisedAt = finding.RaisedAt
                }));
        }
    }
}
