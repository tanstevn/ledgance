using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Engagement.Application.Engagements {
    [RequiresPermission(AuditEngagementPermissions.Manage)]
    public class CreateEngagementCommand : ICommand<Result<CreateEngagementCommandResult>> {
        public Guid ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public EngagementType Type { get; set; }
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public DateOnly? FiscalYearEnd { get; set; }
        public decimal BudgetHours { get; set; }
    }

    public class CreateEngagementCommandResult {
        public Guid Id { get; set; }
    }

    public class CreateEngagementCommandValidator : AbstractValidator<CreateEngagementCommand> {
        public CreateEngagementCommandValidator() {
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.BudgetHours).GreaterThanOrEqualTo(0);
        }
    }

    public class CreateEngagementCommandHandler
        : IRequestHandler<CreateEngagementCommand, Result<CreateEngagementCommandResult>> {
        private readonly IEngagementRepository _engagements;
        private readonly ITeamRepository _team;
        private readonly IClientLookup _clients;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public CreateEngagementCommandHandler(IEngagementRepository engagements,
            ITeamRepository team, IClientLookup clients, IEntitlementService entitlements,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _engagements = engagements;
            _team = team;
            _clients = clients;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<CreateEngagementCommandResult>> HandleAsync(
            CreateEngagementCommand request, CancellationToken ct) {
            var user = _currentUser.Require();

            if (!await _clients.ExistsActiveAsync(request.ClientId, ct)) {
                return Result<CreateEngagementCommandResult>
                    .Error("The client does not exist or is archived.");
            }

            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                ProductModule.Audit, ct);
            var active = await _engagements.CountActiveAsync(ct);
            entitlements.RequireWithinLimit(Entitlements.MaxEngagements, active + 1);

            var engagement = DomainEngagement.Create(request.ClientId, request.Name,
                request.Type, request.PeriodStart, request.PeriodEnd, request.FiscalYearEnd,
                request.BudgetHours, user.UserId);

            await _engagements.AddAsync(engagement, ct);

            // The creator carries partner responsibility until the team is staffed — an
            // engagement must never exist without a partner able to sign it off.
            await _team.AddAsync(EngagementTeamMember.Assign(engagement.Id, user.UserId,
                EngagementRole.Partner), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "engagement.created",
                "Engagement", engagement.Id, $"Engagement '{engagement.Name}' was created.",
                engagement.Id), ct);

            return Result<CreateEngagementCommandResult>
                .Success(new CreateEngagementCommandResult { Id = engagement.Id });
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Manage)]
    public class UpdateEngagementCommand : ICommand<Result<bool>> {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public EngagementType Type { get; set; }
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public DateOnly? FiscalYearEnd { get; set; }
        public decimal BudgetHours { get; set; }
    }

    public class UpdateEngagementCommandValidator : AbstractValidator<UpdateEngagementCommand> {
        public UpdateEngagementCommandValidator() {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.BudgetHours).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateEngagementCommandHandler
        : IRequestHandler<UpdateEngagementCommand, Result<bool>> {
        private readonly IEngagementRepository _engagements;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public UpdateEngagementCommandHandler(IEngagementRepository engagements,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _engagements = engagements;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateEngagementCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.Id, ct);

            var engagement = await _engagements.FindAsync(request.Id, ct);

            if (engagement is null) {
                return Result<bool>.Error("Engagement was not found.");
            }

            engagement.UpdateDetails(request.Name, request.Type, request.PeriodStart,
                request.PeriodEnd, request.FiscalYearEnd, request.BudgetHours);

            await _engagements.UpdateAsync(engagement, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "engagement.updated",
                "Engagement", engagement.Id, $"Engagement '{engagement.Name}' was updated.",
                engagement.Id), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class ChangeEngagementStatusCommand : ICommand<Result<string>> {
        public Guid Id { get; set; }
        public EngagementStatus TargetStatus { get; set; }
    }

    public class ChangeEngagementStatusCommandValidator
        : AbstractValidator<ChangeEngagementStatusCommand> {
        public ChangeEngagementStatusCommandValidator() {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public class ChangeEngagementStatusCommandHandler
        : IRequestHandler<ChangeEngagementStatusCommand, Result<string>> {
        private readonly IEngagementRepository _engagements;
        private readonly IEngagementProgressReader _progress;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public ChangeEngagementStatusCommandHandler(IEngagementRepository engagements,
            IEngagementProgressReader progress, IEngagementAccessGuard access,
            IActivityRecorder activity) {
            _engagements = engagements;
            _progress = progress;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<string>> HandleAsync(ChangeEngagementStatusCommand request,
            CancellationToken ct) {
            var access = await _access.EnsureMemberAsync(request.Id, ct);

            var engagement = await _engagements.FindAsync(request.Id, ct);

            if (engagement is null) {
                return Result<string>.Error("Engagement was not found.");
            }

            switch (request.TargetStatus) {
                case EngagementStatus.Fieldwork:
                    engagement.StartFieldwork();
                    break;
                case EngagementStatus.Review:
                    engagement.SubmitForReview(await _progress.GetAsync(engagement.Id, ct));
                    break;
                case EngagementStatus.SignedOff:
                    engagement.SignOff(await _progress.GetAsync(engagement.Id, ct),
                        access.TeamRole);
                    break;
                case EngagementStatus.Completed:
                    engagement.Complete(await _progress.GetAsync(engagement.Id, ct));
                    break;
                default:
                    return Result<string>.Error("The requested status is not a valid target.");
            }

            await _engagements.UpdateAsync(engagement, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "engagement.status_changed",
                "Engagement", engagement.Id,
                $"Engagement '{engagement.Name}' moved to {engagement.Status}.",
                engagement.Id), ct);

            return Result<string>.Success(engagement.Status.ToString());
        }
    }
}
