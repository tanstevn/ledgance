using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum EngagementRole { Staff, Senior, Manager, Partner }

    public sealed record EngagementTeamMember(
        Guid Id,
        Guid EngagementId,
        Guid UserId,
        EngagementRole Role,
        DateTime AssignedAt) {
        public static EngagementTeamMember Assign(Guid engagementId, Guid userId,
            EngagementRole role) =>
            new(Guid.NewGuid(), engagementId, userId, role, DateTime.UtcNow);
    }

    public static class TeamRules {
        public static void EnsureCanAssign(IReadOnlyCollection<EngagementTeamMember> team,
            Guid userId) {
            if (team.Any(member => member.UserId == userId)) {
                throw new DomainRuleException(
                    "This user is already assigned to the engagement team.");
            }
        }

        public static void EnsureCanRemove(IReadOnlyCollection<EngagementTeamMember> team,
            Guid memberId) {
            var member = team.FirstOrDefault(m => m.Id == memberId)
                ?? throw new DomainRuleException("The team member was not found.");

            if (member.Role == EngagementRole.Partner
                && team.Count(m => m.Role == EngagementRole.Partner) == 1) {
                throw new DomainRuleException(
                    "An engagement must keep at least one partner assigned.");
            }
        }
    }
}
