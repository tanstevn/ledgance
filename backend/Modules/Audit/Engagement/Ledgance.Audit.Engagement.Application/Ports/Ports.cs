using Ledgance.Audit.Engagement.Domain;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;
using DomainEvidence = Ledgance.Audit.Engagement.Domain.Evidence;

namespace Ledgance.Audit.Engagement.Application.Ports {
    public sealed record EngagementPage(IReadOnlyList<DomainEngagement> Rows, long TotalCount);

    public interface IEngagementRepository {
        Task<DomainEngagement?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<DomainEngagement>> ListAsync(Guid? clientId, CancellationToken ct);
        Task<EngagementPage> ListPageAsync(Guid? clientId, EngagementStatus? status,
            string? search, int page, int pageSize, CancellationToken ct);
        Task<long> CountActiveAsync(CancellationToken ct);
        Task AddAsync(DomainEngagement engagement, CancellationToken ct);
        Task UpdateAsync(DomainEngagement engagement, CancellationToken ct);
    }

    public interface ITeamRepository {
        Task<IReadOnlyList<EngagementTeamMember>> ListAsync(Guid engagementId, CancellationToken ct);
        Task<EngagementTeamMember?> FindForUserAsync(Guid engagementId, Guid userId, CancellationToken ct);
        Task<IReadOnlyList<Guid>> ListEngagementIdsForUserAsync(Guid userId, CancellationToken ct);
        Task AddAsync(EngagementTeamMember member, CancellationToken ct);
        Task RemoveAsync(Guid memberId, CancellationToken ct);
    }

    public interface IRiskRepository {
        Task<Risk?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<Risk>> ListAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(Risk risk, CancellationToken ct);
        Task UpdateAsync(Risk risk, CancellationToken ct);
    }

    public interface IProcedureRepository {
        Task<AuditProcedure?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<AuditProcedure>> ListAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(AuditProcedure procedure, CancellationToken ct);
        Task UpdateAsync(AuditProcedure procedure, CancellationToken ct);
    }

    public interface IWorkingPaperRepository {
        Task<WorkingPaper?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<WorkingPaper>> ListAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(WorkingPaper paper, CancellationToken ct);
        Task UpdateAsync(WorkingPaper paper, CancellationToken ct);
    }

    public interface IEvidenceRepository {
        Task<DomainEvidence?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<DomainEvidence>> ListAsync(Guid engagementId, CancellationToken ct);
        Task<DomainEvidence?> FindByFileNameAsync(Guid engagementId, string fileName,
            CancellationToken ct);
        Task<long> SumSizeBytesAsync(CancellationToken ct);
        Task AddAsync(DomainEvidence evidence, CancellationToken ct);
        Task UpdateAsync(DomainEvidence evidence, CancellationToken ct);
    }

    public interface IFindingRepository {
        Task<Finding?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<Finding>> ListAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(Finding finding, CancellationToken ct);
        Task UpdateAsync(Finding finding, CancellationToken ct);
    }

    public interface IReportRepository {
        Task<AuditReport?> FindByEngagementAsync(Guid engagementId, CancellationToken ct);
        Task UpsertAsync(AuditReport report, CancellationToken ct);
    }

    public interface ITrialBalanceRepository {
        Task<TrialBalanceImport?> FindLatestAsync(Guid engagementId, CancellationToken ct);
        Task AddAsync(TrialBalanceImport import, CancellationToken ct);
    }

    /// <summary>
    /// Computes the stage-gate snapshot the Engagement aggregate needs for status transitions.
    /// </summary>
    public interface IEngagementProgressReader {
        Task<EngagementProgress> GetAsync(Guid engagementId, CancellationToken ct);
    }

    /// <summary>
    /// Client existence check owned by the Engagement feature, so creating an engagement does not
    /// couple this module to the Client feature's application layer.
    /// </summary>
    public interface IClientLookup {
        Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken ct);
        Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> clientIds, CancellationToken ct);
    }

    public interface IEvidenceFileStore {
        Task<string> UploadAsync(Guid engagementId, Guid evidenceId, int version,
            string fileName, byte[] content, string contentType, CancellationToken ct);
        Task<string> CreateDownloadUrlAsync(string storagePath, TimeSpan lifetime, CancellationToken ct);
    }
}
