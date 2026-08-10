using Ledgance.Audit.Client.Domain;

namespace Ledgance.Audit.Client.Application.Ports {
    public sealed record ClientPage(IReadOnlyList<AuditClient> Rows, long TotalCount);

    public interface IClientRepository {
        Task<AuditClient?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<AuditClient>> ListAsync(bool includeArchived, CancellationToken ct);
        Task<ClientPage> ListPageAsync(int page, int pageSize, string? search, CancellationToken ct);
        Task<long> CountActiveAsync(CancellationToken ct);
        Task<AuditClient> AddAsync(AuditClient client, CancellationToken ct);
        Task UpdateAsync(AuditClient client, CancellationToken ct);
    }

    /// <summary>
    /// Owned by the Client feature so archiving can refuse clients that still have active
    /// engagements without referencing the Engagement feature's internals.
    /// </summary>
    public interface IClientEngagementCounter {
        Task<int> CountActiveEngagementsAsync(Guid clientId, CancellationToken ct);
    }
}
