using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Planning {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class SaveAuditPlanCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public DateOnly? TimelineStart { get; set; }
        public DateOnly? TimelineEnd { get; set; }
    }

    public class SaveAuditPlanCommandValidator : AbstractValidator<SaveAuditPlanCommand> {
        public SaveAuditPlanCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Scope).NotEmpty();
            RuleFor(x => x.Objectives).NotEmpty();
        }
    }

    public class SaveAuditPlanCommandHandler : IRequestHandler<SaveAuditPlanCommand, Result<bool>> {
        private readonly IEngagementRepository _engagements;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public SaveAuditPlanCommandHandler(IEngagementRepository engagements,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _engagements = engagements;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(SaveAuditPlanCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var engagement = await _engagements.FindAsync(request.EngagementId, ct);

            if (engagement is null) {
                return Result<bool>.Error("Engagement was not found.");
            }

            engagement.SavePlan(request.Scope, request.Objectives, request.Strategy,
                request.TimelineStart, request.TimelineEnd);

            await _engagements.UpdateAsync(engagement, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "plan.saved",
                "Engagement", engagement.Id, "The audit plan was updated.",
                engagement.Id), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Approve)]
    public class ApproveAuditPlanCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
    }

    public class ApproveAuditPlanCommandValidator : AbstractValidator<ApproveAuditPlanCommand> {
        public ApproveAuditPlanCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class ApproveAuditPlanCommandHandler
        : IRequestHandler<ApproveAuditPlanCommand, Result<bool>> {
        private readonly IEngagementRepository _engagements;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ApproveAuditPlanCommandHandler(IEngagementRepository engagements,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _engagements = engagements;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ApproveAuditPlanCommand request,
            CancellationToken ct) {
            var access = await _access.EnsureMemberAsync(request.EngagementId, ct);

            if (access.TeamRole is not (EngagementRole.Manager or EngagementRole.Partner)) {
                throw new DomainRuleException(
                    "Only an engagement manager or partner can approve the audit plan.");
            }

            var engagement = await _engagements.FindAsync(request.EngagementId, ct);

            if (engagement is null) {
                return Result<bool>.Error("Engagement was not found.");
            }

            engagement.ApprovePlan(_currentUser.Require().UserId);
            await _engagements.UpdateAsync(engagement, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "plan.approved",
                "Engagement", engagement.Id, "The audit plan was approved.",
                engagement.Id), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class SetMaterialityCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public decimal OverallAmount { get; set; }
        public decimal PerformanceAmount { get; set; }
        public decimal ClearlyTrivialThreshold { get; set; }
        public string Basis { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
    }

    public class SetMaterialityCommandValidator : AbstractValidator<SetMaterialityCommand> {
        public SetMaterialityCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Basis).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Rationale).NotEmpty();
        }
    }

    public class SetMaterialityCommandHandler
        : IRequestHandler<SetMaterialityCommand, Result<bool>> {
        private readonly IEngagementRepository _engagements;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public SetMaterialityCommandHandler(IEngagementRepository engagements,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _engagements = engagements;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(SetMaterialityCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var engagement = await _engagements.FindAsync(request.EngagementId, ct);

            if (engagement is null) {
                return Result<bool>.Error("Engagement was not found.");
            }

            engagement.SetMateriality(Materiality.Create(request.OverallAmount,
                request.PerformanceAmount, request.ClearlyTrivialThreshold,
                request.Basis, request.Rationale));

            await _engagements.UpdateAsync(engagement, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "materiality.set",
                "Engagement", engagement.Id, "Materiality was determined.",
                engagement.Id), ct);

            return Result<bool>.Success(true);
        }
    }
}
