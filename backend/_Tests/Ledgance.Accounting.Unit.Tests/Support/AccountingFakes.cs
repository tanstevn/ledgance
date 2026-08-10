using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Activity;

namespace Ledgance.Accounting.Unit.Tests.Support {
    public sealed class RecordingActivityRecorder : IActivityRecorder {
        public List<ActivityEntry> Entries { get; } = [];

        public Task RecordAsync(ActivityEntry entry, CancellationToken ct) {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    public sealed class InMemoryEntityRepository : IEntityRepository {
        public List<AccountingEntity> Entities { get; } = [];

        public Task<AccountingEntity?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Entities.FirstOrDefault(entity => entity.Id == id));

        public Task<IReadOnlyList<AccountingEntity>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccountingEntity>>(Entities.ToList());

        public Task<long> CountActiveAsync(CancellationToken ct) =>
            Task.FromResult((long)Entities.Count(entity => !entity.IsArchived));

        public Task AddAsync(AccountingEntity entity, CancellationToken ct) {
            Entities.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AccountingEntity entity, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryAccountRepository : IAccountRepository {
        public List<Account> Accounts { get; } = [];

        public Task<Account?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Accounts.FirstOrDefault(account => account.Id == id));

        public Task<IReadOnlyList<Account>> ListAsync(Guid entityId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Account>>(Accounts
                .Where(account => account.EntityId == entityId)
                .ToList());

        public Task<bool> CodeExistsAsync(Guid entityId, string code, Guid? exceptAccountId,
            CancellationToken ct) =>
            Task.FromResult(Accounts.Any(account => account.EntityId == entityId
                && account.Code == code && account.Id != exceptAccountId));

        public Task<bool> HasChildrenAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(Accounts.Any(account => account.ParentAccountId == accountId));

        public Task AddAsync(Account account, CancellationToken ct) {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Account account, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryFiscalPeriodRepository : IFiscalPeriodRepository {
        public List<FiscalPeriod> Periods { get; } = [];

        public Task<FiscalPeriod?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Periods.FirstOrDefault(period => period.Id == id));

        public Task<IReadOnlyList<FiscalPeriod>> ListAsync(Guid entityId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FiscalPeriod>>(Periods
                .Where(period => period.EntityId == entityId)
                .ToList());

        public Task<FiscalPeriod?> FindContainingAsync(Guid entityId, DateOnly date,
            CancellationToken ct) =>
            Task.FromResult(Periods.FirstOrDefault(period =>
                period.EntityId == entityId && period.Contains(date)));

        public Task<bool> AnyOpenAsync(Guid entityId, CancellationToken ct) =>
            Task.FromResult(Periods.Any(period =>
                period.EntityId == entityId && period.IsOpen));

        public Task AddAsync(FiscalPeriod period, CancellationToken ct) {
            Periods.Add(period);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FiscalPeriod period, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryJournalEntryRepository : IJournalEntryRepository {
        public List<JournalEntry> Entries { get; } = [];

        public Task<JournalEntry?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Entries.FirstOrDefault(entry => entry.Id == id));

        public Task<JournalEntryPage> ListPageAsync(Guid entityId, JournalEntryStatus? status,
            DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct) {
            var filtered = Entries
                .Where(entry => entry.EntityId == entityId)
                .Where(entry => status is null || entry.Status == status)
                .Where(entry => from is null || entry.EntryDate >= from)
                .Where(entry => to is null || entry.EntryDate <= to)
                .OrderByDescending(entry => entry.EntryNumber)
                .ToList();

            return Task.FromResult(new JournalEntryPage(filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(), filtered.Count));
        }

        public Task<long> NextEntryNumberAsync(Guid entityId, CancellationToken ct) =>
            Task.FromResult(Entries
                .Where(entry => entry.EntityId == entityId)
                .Select(entry => entry.EntryNumber)
                .DefaultIfEmpty(0)
                .Max() + 1);

        public Task<long> CountInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct) =>
            Task.FromResult((long)Entries.Count(entry => entry.EntityId == entityId
                && entry.EntryDate >= from && entry.EntryDate <= to));

        public Task<bool> HasDraftsInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct) =>
            Task.FromResult(Entries.Any(entry => entry.EntityId == entityId
                && entry.Status == JournalEntryStatus.Draft
                && entry.EntryDate >= from && entry.EntryDate <= to));

        public Task AddAsync(JournalEntry entry, CancellationToken ct) {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(JournalEntry entry, CancellationToken ct) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct) {
            Entries.RemoveAll(entry => entry.Id == id);
            return Task.CompletedTask;
        }
    }

    public sealed class InMemoryLedgerLineRepository : ILedgerLineRepository {
        public List<(Guid EntityId, PostedLedgerLine Line)> Lines { get; } = [];

        public Task AddRangeAsync(IReadOnlyList<PostedLedgerLine> lines, Guid entityId,
            CancellationToken ct) {
            Lines.AddRange(lines.Select(line => (entityId, line)));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PostedLedgerLine>> ListByAccountAsync(Guid entityId,
            Guid accountId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PostedLedgerLine>>(Lines
                .Where(item => item.EntityId == entityId
                    && item.Line.AccountId == accountId
                    && (from is null || item.Line.EntryDate >= from)
                    && (to is null || item.Line.EntryDate <= to))
                .Select(item => item.Line)
                .ToList());

        public Task<IReadOnlyList<PostedLedgerLine>> ListForEntityAsync(Guid entityId,
            DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PostedLedgerLine>>(Lines
                .Where(item => item.EntityId == entityId
                    && (from is null || item.Line.EntryDate >= from)
                    && (to is null || item.Line.EntryDate <= to))
                .Select(item => item.Line)
                .ToList());

        public Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(Lines.Any(item => item.Line.AccountId == accountId));
    }

    public sealed class InMemoryReconciliationRepository : IReconciliationRepository {
        public List<Reconciliation> Reconciliations { get; } = [];

        public Task<Reconciliation?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Reconciliations
                .FirstOrDefault(reconciliation => reconciliation.Id == id));

        public Task<IReadOnlyList<Reconciliation>> ListAsync(Guid entityId, Guid? accountId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Reconciliation>>(Reconciliations
                .Where(reconciliation => reconciliation.EntityId == entityId
                    && (accountId is null || reconciliation.AccountId == accountId))
                .ToList());

        public Task<bool> HasInProgressForAccountAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(Reconciliations.Any(reconciliation =>
                reconciliation.AccountId == accountId
                && reconciliation.Status == ReconciliationStatus.InProgress));

        public Task AddAsync(Reconciliation reconciliation, CancellationToken ct) {
            Reconciliations.Add(reconciliation);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Reconciliation reconciliation, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public sealed class InMemoryDocumentRepository : IDocumentRepository {
        public List<AccountingDocument> Documents { get; } = [];

        public Task<AccountingDocument?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Documents.FirstOrDefault(document => document.Id == id));

        public Task<IReadOnlyList<AccountingDocument>> ListAsync(Guid entityId,
            Guid? journalEntryId, Guid? reconciliationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccountingDocument>>(Documents
                .Where(document => document.EntityId == entityId
                    && (journalEntryId is null || document.JournalEntryId == journalEntryId)
                    && (reconciliationId is null
                        || document.ReconciliationId == reconciliationId))
                .ToList());

        public Task<long> SumSizeBytesAsync(CancellationToken ct) =>
            Task.FromResult(Documents.Sum(document => document.SizeBytes));

        public Task AddAsync(AccountingDocument document, CancellationToken ct) {
            Documents.Add(document);
            return Task.CompletedTask;
        }
    }

    public sealed class FakeDocumentFileStore : IDocumentFileStore {
        public List<string> UploadedPaths { get; } = [];

        public Task<string> UploadAsync(Guid entityId, Guid documentId, string fileName,
            byte[] content, string contentType, CancellationToken ct) {
            var path = $"{entityId}/{documentId}/{fileName}";
            UploadedPaths.Add(path);
            return Task.FromResult(path);
        }

        public Task<string> CreateDownloadUrlAsync(string storagePath, TimeSpan lifetime,
            CancellationToken ct) =>
            Task.FromResult($"https://storage.ledgance.test/{storagePath}?signed=true");
    }
}
