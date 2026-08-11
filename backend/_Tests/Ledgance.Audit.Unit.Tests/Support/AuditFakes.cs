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
            CancellationToken ct) {
            var matching = Clients
                .Where(client => string.IsNullOrWhiteSpace(search)
                    || client.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(client => client.Name)
                .ToList();

            var rows = matching
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new ClientPage(rows, matching.Count));
        }

        public Task<long> CountActiveAsync(CancellationToken ct) =>
            Task.FromResult((long)Clients.Count(client => !client.IsArchived));

        public Task<DomainClient> AddAsync(DomainClient client, CancellationToken ct) {
            Clients.Add(client);
            return Task.FromResult(client);
        }

        public Task UpdateAsync(DomainClient client, CancellationToken ct) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Stands in for the Engagement module's counter, which the Client feature only knows
    /// through its own port.
    /// </summary>
    public sealed class InMemoryClientEngagementCounter : IClientEngagementCounter {
        public Dictionary<Guid, ClientEngagementCounts> Counts { get; } = [];

        public Task<int> CountActiveEngagementsAsync(Guid clientId, CancellationToken ct) =>
            Task.FromResult(Counts.GetValueOrDefault(clientId,
                new ClientEngagementCounts(0, 0)).Active);

        public Task<IReadOnlyDictionary<Guid, ClientEngagementCounts>> CountForClientsAsync(
            IEnumerable<Guid> clientIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ClientEngagementCounts>>(clientIds
                .Distinct()
                .Where(Counts.ContainsKey)
                .ToDictionary(id => id, id => Counts[id]));
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

        public Task<EngagementPage> ListPageAsync(Guid? clientId, EngagementStatus? status,
            string? search, int page, int pageSize, CancellationToken ct) {
            var matching = Engagements
                .Where(engagement => clientId is null || engagement.ClientId == clientId)
                .Where(engagement => status is null || engagement.Status == status)
                .Where(engagement => string.IsNullOrWhiteSpace(search)
                    || engagement.Name.Contains(search.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(engagement => engagement.CreatedAt)
                .ToList();

            var rows = matching
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new EngagementPage(rows, matching.Count));
        }

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

        public Task<IReadOnlyList<Guid>> ListEngagementIdsForUserAsync(Guid userId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(Members
                .Where(member => member.UserId == userId)
                .Select(member => member.EngagementId)
                .Distinct()
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

    public sealed class InMemoryRiskRepository : IRiskRepository {
        public List<Risk> Risks { get; } = [];

        public Task<Risk?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Risks.FirstOrDefault(risk => risk.Id == id));

        public Task<IReadOnlyList<Risk>> ListAsync(Guid engagementId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Risk>>(Risks
                .Where(risk => risk.EngagementId == engagementId)
                .ToList());

        public Task AddAsync(Risk risk, CancellationToken ct) {
            Risks.Add(risk);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Risk risk, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryTrialBalanceRepository : ITrialBalanceRepository {
        public List<TrialBalanceImport> Imports { get; } = [];

        public Task<TrialBalanceImport?> FindLatestAsync(Guid engagementId,
            CancellationToken ct) =>
            Task.FromResult(Imports
                .Where(import => import.EngagementId == engagementId)
                .OrderByDescending(import => import.ImportedAt)
                .FirstOrDefault());

        public Task AddAsync(TrialBalanceImport import, CancellationToken ct) {
            Imports.Add(import);
            return Task.CompletedTask;
        }
    }

    public sealed class StubOrganizationDirectory : IOrganizationDirectory {
        public List<OrganizationMemberInfo> Members { get; } = [];

        public Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(Members.Any(member => member.UserId == userId));

        public Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail, string? product,
            CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());

        public Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(
            Guid organizationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrganizationMemberInfo>>(Members);

        public Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId, Guid userId,
            CancellationToken ct) =>
            Task.FromResult(Members.FirstOrDefault(member => member.UserId == userId));

        public Task<OrganizationInfo?> GetOrganizationAsync(Guid organizationId,
            CancellationToken ct) =>
            Task.FromResult<OrganizationInfo?>(
                new OrganizationInfo("Test Organization", ["Audit", "Accounting"]));

        public Task AddProductAsync(Guid organizationId, string product,
            CancellationToken ct) =>
            Task.CompletedTask;
    }
}
