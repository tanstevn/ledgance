using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Team {
    [RequiresPermission(AuditEngagementPermissions.Manage)]
    public class AssignTeamMemberCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public Guid UserId { get; set; }
        public EngagementRole Role { get; set; }
    }

    public class AssignTeamMemberCommandValidator : AbstractValidator<AssignTeamMemberCommand> {
        public AssignTeamMemberCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class AssignTeamMemberCommandHandler
        : IRequestHandler<AssignTeamMemberCommand, Result<Guid>> {
        private readonly ITeamRepository _team;
        private readonly IEngagementAccessGuard _access;
        private readonly IOrganizationDirectory _directory;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public AssignTeamMemberCommandHandler(ITeamRepository team,
            IEngagementAccessGuard access, IOrganizationDirectory directory,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _team = team;
            _access = access;
            _directory = directory;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(AssignTeamMemberCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var member = await _directory.FindMemberAsync(
                _currentUser.RequireOrganizationId(), request.UserId, ct);

            if (member is null) {
                return Result<Guid>.Error(
                    "The user is not a member of this organization.");
            }

            var team = await _team.ListAsync(request.EngagementId, ct);
            TeamRules.EnsureCanAssign(team, request.UserId);

            var assignment = EngagementTeamMember.Assign(request.EngagementId,
                request.UserId, request.Role);
            await _team.AddAsync(assignment, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "engagement.team_assigned",
                "Engagement", request.EngagementId,
                $"{member.DisplayName} was assigned as {request.Role}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(assignment.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Manage)]
    public class RemoveTeamMemberCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public Guid MemberId { get; set; }
    }

    public class RemoveTeamMemberCommandValidator : AbstractValidator<RemoveTeamMemberCommand> {
        public RemoveTeamMemberCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.MemberId).NotEmpty();
        }
    }

    public class RemoveTeamMemberCommandHandler
        : IRequestHandler<RemoveTeamMemberCommand, Result<bool>> {
        private readonly ITeamRepository _team;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public RemoveTeamMemberCommandHandler(ITeamRepository team,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _team = team;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(RemoveTeamMemberCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var team = await _team.ListAsync(request.EngagementId, ct);
            TeamRules.EnsureCanRemove(team, request.MemberId);

            await _team.RemoveAsync(request.MemberId, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "engagement.team_removed",
                "Engagement", request.EngagementId, "A team member was removed.",
                request.EngagementId), ct);

            return Result<bool>.Success(true);
        }
    }
}
