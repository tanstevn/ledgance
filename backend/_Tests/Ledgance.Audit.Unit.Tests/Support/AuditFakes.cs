using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Identity;
using DomainClient = Ledgance.Audit.Client.Domain.AuditClient;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Support {
    public sealed class RecordingActivityRecorder : IActivityRecorder {
        public List<ActivityEntry> Entries { get; } = [];

        public Task RecordAsync(ActivityEntry entry, CancellationToken ct) {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    public sealed class InMemoryClientRepository : IClientRepository {
        public List<DomainClient> Clients { get; } = [];

        public Task<DomainClient?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Clients.FirstOrDefault(client => client.Id == id));

        public Task<IReadOnlyList<DomainClient>> ListAsync(bool includeArchived,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DomainClient>>(Clients
                .Where(client => includeArchived || !client.IsArchived)
                .ToList());

        public Task<ClientPage> ListPageAsync(int page, int pageSize, string? search,
            CancellationToken ct) =>
            Task.FromResult(new ClientPage(Clients, Clients.Count));

        public Task<long> CountActiveAsync(CancellationToken ct) =>
            Task.FromResult((long)Clients.Count(client => !client.IsArchived));

        public Task<DomainClient> AddAsync(DomainClient client, CancellationToken ct) {
            Clients.Add(client);
            return Task.FromResult(client);
        }

        public Task UpdateAsync(DomainClient client, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryEngagementRepository : IEngagementRepository {
        public List<DomainEngagement> Engagements { get; } = [];

        public Task<DomainEngagement?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Engagements.FirstOrDefault(engagement => engagement.Id == id));

        public Task<IReadOnlyList<DomainEngagement>> ListAsync(Guid? clientId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DomainEngagement>>(Engagements
                .Where(engagement => clientId is null || engagement.ClientId == clientId)
                .ToList());

        public Task<long> CountActiveAsync(CancellationToken ct) =>
            Task.FromResult((long)Engagements.Count(engagement => engagement.IsActive));

        public Task AddAsync(DomainEngagement engagement, CancellationToken ct) {
            Engagements.Add(engagement);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainEngagement engagement, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryTeamRepository : ITeamRepository {
        public List<EngagementTeamMember> Members { get; } = [];

        public Task<IReadOnlyList<EngagementTeamMember>> ListAsync(Guid engagementId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EngagementTeamMember>>(Members
                .Where(member => member.EngagementId == engagementId)
                .ToList());

        public Task<EngagementTeamMember?> FindForUserAsync(Guid engagementId, Guid userId,
            CancellationToken ct) =>
            Task.FromResult(Members.FirstOrDefault(member =>
                member.EngagementId == engagementId && member.UserId == userId));

        public Task AddAsync(EngagementTeamMember member, CancellationToken ct) {
            Members.Add(member);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid memberId, CancellationToken ct) {
            Members.RemoveAll(member => member.Id == memberId);
            return Task.CompletedTask;
        }
    }

    public sealed class StubClientLookup : IClientLookup {
        public HashSet<Guid> ActiveClients { get; } = [];

        public Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken ct) =>
            Task.FromResult(ActiveClients.Contains(clientId));

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
            IEnumerable<Guid> clientIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(clientIds
                .ToDictionary(id => id, _ => "Client"));
    }

    public sealed class StubProgressReader : IEngagementProgressReader {
        public EngagementProgress Progress { get; set; } = new(0, 0, 0, 0, 0, true);

        public Task<EngagementProgress> GetAsync(Guid engagementId, CancellationToken ct) =>
            Task.FromResult(Progress);
    }

    public sealed class StubOrganizationDirectory : IOrganizationDirectory {
        public List<OrganizationMemberInfo> Members { get; } = [];

        public Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(Members.Any(member => member.UserId == userId));

        public Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail,
            CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());

        public Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(
            Guid organizationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrganizationMemberInfo>>(Members);

        public Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId, Guid userId,
            CancellationToken ct) =>
            Task.FromResult(Members.FirstOrDefault(member => member.UserId == userId));
    }
}
