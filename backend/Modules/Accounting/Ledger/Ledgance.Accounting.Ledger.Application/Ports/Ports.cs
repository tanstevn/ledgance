using Ledgance.Accounting.Ledger.Domain;

namespace Ledgance.Accounting.Ledger.Application.Ports {
    public interface IEntityRepository {
        Task<AccountingEntity?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<AccountingEntity>> ListAsync(CancellationToken ct);
        Task<long> CountActiveAsync(CancellationToken ct);
        Task AddAsync(AccountingEntity entity, CancellationToken ct);
        Task UpdateAsync(AccountingEntity entity, CancellationToken ct);
    }

    public interface IAccountRepository {
        Task<Account?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<Account>> ListAsync(Guid entityId, CancellationToken ct);
        Task<bool> CodeExistsAsync(Guid entityId, string code, Guid? exceptAccountId,
            CancellationToken ct);
        Task<bool> HasChildrenAsync(Guid accountId, CancellationToken ct);
        Task AddAsync(Account account, CancellationToken ct);
        Task UpdateAsync(Account account, CancellationToken ct);
    }

    public interface IFiscalPeriodRepository {
        Task<FiscalPeriod?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<FiscalPeriod>> ListAsync(Guid entityId, CancellationToken ct);
        Task<FiscalPeriod?> FindContainingAsync(Guid entityId, DateOnly date,
            CancellationToken ct);
        Task<bool> AnyOpenAsync(Guid entityId, CancellationToken ct);
        Task AddAsync(FiscalPeriod period, CancellationToken ct);
        Task UpdateAsync(FiscalPeriod period, CancellationToken ct);
    }

    public sealed record JournalEntryPage(IReadOnlyList<JournalEntry> Rows, long TotalCount);

    public interface IJournalEntryRepository {
        Task<JournalEntry?> FindAsync(Guid id, CancellationToken ct);
        Task<JournalEntryPage> ListPageAsync(Guid entityId, JournalEntryStatus? status,
            DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
        Task<long> NextEntryNumberAsync(Guid entityId, CancellationToken ct);
        Task<long> CountInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct);
        Task<bool> HasDraftsInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct);
        Task AddAsync(JournalEntry entry, CancellationToken ct);
        Task UpdateAsync(JournalEntry entry, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);
    }

    public interface ILedgerLineRepository {
        Task AddRangeAsync(IReadOnlyList<PostedLedgerLine> lines, Guid entityId,
            CancellationToken ct);
        Task<IReadOnlyList<PostedLedgerLine>> ListByAccountAsync(Guid entityId, Guid accountId,
            DateOnly? from, DateOnly? to, CancellationToken ct);
        Task<IReadOnlyList<PostedLedgerLine>> ListForEntityAsync(Guid entityId, DateOnly? from,
            DateOnly? to, CancellationToken ct);
        Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct);
    }

    public interface IReconciliationRepository {
        Task<Reconciliation?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<Reconciliation>> ListAsync(Guid entityId, Guid? accountId,
            CancellationToken ct);
        Task<bool> HasInProgressForAccountAsync(Guid accountId, CancellationToken ct);
        Task AddAsync(Reconciliation reconciliation, CancellationToken ct);
        Task UpdateAsync(Reconciliation reconciliation, CancellationToken ct);
    }

    public interface IDocumentRepository {
        Task<AccountingDocument?> FindAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<AccountingDocument>> ListAsync(Guid entityId, Guid? journalEntryId,
            Guid? reconciliationId, CancellationToken ct);
        Task<long> SumSizeBytesAsync(CancellationToken ct);
        Task AddAsync(AccountingDocument document, CancellationToken ct);
    }

    public interface IDocumentFileStore {
        Task<string> UploadAsync(Guid entityId, Guid documentId, string fileName,
            byte[] content, string contentType, CancellationToken ct);
        Task<string> CreateDownloadUrlAsync(string storagePath, TimeSpan lifetime,
            CancellationToken ct);
    }
}
