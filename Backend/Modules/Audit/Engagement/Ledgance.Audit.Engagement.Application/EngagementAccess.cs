using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;

namespace Ledgance.Audit.Engagement.Application {
    public sealed record EngagementAccess(EngagementRole? TeamRole, bool ViaOrganizationRole) {
        public bool IsTeamMember => TeamRole is not null;
    }

    public interface IEngagementAccessGuard {
        /// <summary>
        /// Engagement content is confined to the assigned team; organization Admins and Owners
        /// retain oversight access. Everyone else is rejected server-side regardless of their
        /// organization permissions.
        /// </summary>
        Task<EngagementAccess> EnsureMemberAsync(Guid engagementId, CancellationToken ct);
    }

    public sealed class EngagementAccessGuard : IEngagementAccessGuard {
        private readonly ITeamRepository _team;
        private readonly ICurrentUserAccessor _currentUser;

        public EngagementAccessGuard(ITeamRepository team, ICurrentUserAccessor currentUser) {
            _team = team;
            _currentUser = currentUser;
        }

        public async Task<EngagementAccess> EnsureMemberAsync(Guid engagementId,
            CancellationToken ct) {
            var user = _currentUser.Require();
            var membership = await _team.FindForUserAsync(engagementId, user.UserId, ct);

            if (membership is not null) {
                return new EngagementAccess(membership.Role, ViaOrganizationRole: false);
            }

            if (user.Role >= OrganizationRole.Admin) {
                return new EngagementAccess(null, ViaOrganizationRole: true);
            }

            throw new ForbiddenException(
                "You are not assigned to this engagement.");
        }
    }
}
