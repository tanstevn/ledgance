using Ledgance.Audit.AI.Domain;

namespace Ledgance.Audit.AI.Application.Ports {
    public interface IGeneratedReportRepository {
        Task<GeneratedAuditReport?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<GeneratedAuditReport>> ListAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(GeneratedAuditReport report, CancellationToken ct);
        Task UpdateAsync(GeneratedAuditReport report, CancellationToken ct);
    }
}
