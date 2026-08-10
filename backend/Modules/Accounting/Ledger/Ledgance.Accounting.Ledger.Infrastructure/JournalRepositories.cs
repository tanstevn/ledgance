using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Supabase.Postgrest.Interfaces;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Accounting.Ledger.Infrastructure {
    internal sealed class JournalEntryRepository : IJournalEntryRepository {
        private readonly SupabaseRepository<JournalEntryModel> _repository;

        public JournalEntryRepository(SupabaseRepository<JournalEntryModel> repository) {
            _repository = repository;
        }

        public async Task<JournalEntry?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<JournalEntryPage> ListPageAsync(Guid entityId,
            JournalEntryStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize,
            CancellationToken ct) {
            var query = Filtered(entityId, status, from, to);
            var countQuery = Filtered(entityId, status, from, to);

            var offset = (page - 1) * pageSize;

            var rows = await query
                .Order("entry_number", Constants.Ordering.Descending)
                .Range(offset, offset + pageSize - 1)
                .Get(ct);

            var total = await countQuery.Count(Constants.CountType.Exact, ct);

            return new JournalEntryPage(rows.Models.Select(ToDomain).ToList(), total);
        }

        public async Task<long> NextEntryNumberAsync(Guid entityId, CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Order("entry_number", Constants.Ordering.Descending)
                .Limit(1)
                .Get(ct);

            return (rows.Models.FirstOrDefault()?.EntryNumber ?? 0) + 1;
        }

        public async Task<long> CountInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct) =>
            await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("entry_date", Constants.Operator.GreaterThanOrEqual,
                    from.ToString("yyyy-MM-dd"))
                .Filter("entry_date", Constants.Operator.LessThanOrEqual,
                    to.ToString("yyyy-MM-dd"))
                .Count(Constants.CountType.Exact, ct);

        public async Task<bool> HasDraftsInRangeAsync(Guid entityId, DateOnly from, DateOnly to,
            CancellationToken ct) =>
            await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("status", Constants.Operator.Equals, nameof(JournalEntryStatus.Draft))
                .Filter("entry_date", Constants.Operator.GreaterThanOrEqual,
                    from.ToString("yyyy-MM-dd"))
                .Filter("entry_date", Constants.Operator.LessThanOrEqual,
                    to.ToString("yyyy-MM-dd"))
                .Count(Constants.CountType.Exact, ct) > 0;

        public async Task AddAsync(JournalEntry entry, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(entry), ct);

        public async Task UpdateAsync(JournalEntry entry, CancellationToken ct) {
            var existing = await _repository.GetAsync(entry.Id, ct);
            var model = ToModel(entry);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct) =>
            await _repository.DeleteAsync(id, ct);

        private IPostgrestTable<JournalEntryModel> Filtered(Guid entityId,
            JournalEntryStatus? status, DateOnly? from, DateOnly? to) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString());

            if (status is not null) {
                query = query.Filter("status", Constants.Operator.Equals,
                    status.Value.ToString());
            }

            if (from is not null) {
                query = query.Filter("entry_date", Constants.Operator.GreaterThanOrEqual,
                    from.Value.ToString("yyyy-MM-dd"));
            }

            if (to is not null) {
                query = query.Filter("entry_date", Constants.Operator.LessThanOrEqual,
                    to.Value.ToString("yyyy-MM-dd"));
            }

            return query;
        }

        private static JournalEntry ToDomain(JournalEntryModel model) =>
            JournalEntry.Restore(model.Id, model.EntityId, model.EntryNumber,
                DateOnly.FromDateTime(model.EntryDate), model.Memo, model.Reference,
                Enum.Parse<JournalEntryStatus>(model.Status),
                model.Lines.Select(line => JournalLine.Create(line.AccountId,
                    line.Description, line.Debit, line.Credit)),
                model.ReversalOfEntryId, model.ReversedByEntryId, model.CreatedBy,
                model.CreatedAt, model.PostedBy, model.PostedAt);

        private static JournalEntryModel ToModel(JournalEntry entry) =>
            new() {
                Id = entry.Id,
                EntityId = entry.EntityId,
                EntryNumber = entry.EntryNumber,
                EntryDate = entry.EntryDate.ToDateTime(TimeOnly.MinValue),
                Memo = entry.Memo,
                Reference = entry.Reference,
                Status = entry.Status.ToString(),
                Lines = entry.Lines
                    .Select(line => new JournalLineDoc {
                        AccountId = line.AccountId,
                        Description = line.Description,
                        Debit = line.Debit,
                        Credit = line.Credit
                    })
                    .ToList(),
                ReversalOfEntryId = entry.ReversalOfEntryId,
                ReversedByEntryId = entry.ReversedByEntryId,
                CreatedBy = entry.CreatedBy,
                CreatedAt = entry.CreatedAt,
                PostedBy = entry.PostedBy,
                PostedAt = entry.PostedAt
            };
    }

    internal sealed class LedgerLineRepository : ILedgerLineRepository {
        private readonly SupabaseRepository<LedgerLineModel> _repository;

        public LedgerLineRepository(SupabaseRepository<LedgerLineModel> repository) {
            _repository = repository;
        }

        public async Task AddRangeAsync(IReadOnlyList<PostedLedgerLine> lines, Guid entityId,
            CancellationToken ct) {
            foreach (var line in lines) {
                await _repository.InsertAsync(new LedgerLineModel {
                    Id = Guid.NewGuid(),
                    EntityId = entityId,
                    EntryId = line.EntryId,
                    EntryNumber = line.EntryNumber,
                    EntryDate = line.EntryDate.ToDateTime(TimeOnly.MinValue),
                    AccountId = line.AccountId,
                    Description = line.Description,
                    Debit = line.Debit,
                    Credit = line.Credit
                }, ct);
            }
        }

        public async Task<IReadOnlyList<PostedLedgerLine>> ListByAccountAsync(Guid entityId,
            Guid accountId, DateOnly? from, DateOnly? to, CancellationToken ct) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("account_id", Constants.Operator.Equals, accountId.ToString());

            var rows = await ApplyRange(query, from, to)
                .Order("entry_date", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<IReadOnlyList<PostedLedgerLine>> ListForEntityAsync(Guid entityId,
            DateOnly? from, DateOnly? to, CancellationToken ct) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString());

            var rows = await ApplyRange(query, from, to)
                .Order("entry_date", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct) =>
            await _repository.Query()
                .Filter("account_id", Constants.Operator.Equals, accountId.ToString())
                .Count(Constants.CountType.Exact, ct) > 0;

        private static IPostgrestTable<LedgerLineModel> ApplyRange(
            IPostgrestTable<LedgerLineModel> query, DateOnly? from, DateOnly? to) {
            if (from is not null) {
                query = query.Filter("entry_date", Constants.Operator.GreaterThanOrEqual,
                    from.Value.ToString("yyyy-MM-dd"));
            }

            if (to is not null) {
                query = query.Filter("entry_date", Constants.Operator.LessThanOrEqual,
                    to.Value.ToString("yyyy-MM-dd"));
            }

            return query;
        }

        private static PostedLedgerLine ToDomain(LedgerLineModel model) =>
            new(model.EntryId, model.EntryNumber, DateOnly.FromDateTime(model.EntryDate),
                model.AccountId, model.Description, model.Debit, model.Credit);
    }

    internal sealed class ReconciliationRepository : IReconciliationRepository {
        private readonly SupabaseRepository<ReconciliationModel> _repository;

        public ReconciliationRepository(SupabaseRepository<ReconciliationModel> repository) {
            _repository = repository;
        }

        public async Task<Reconciliation?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<Reconciliation>> ListAsync(Guid entityId,
            Guid? accountId, CancellationToken ct) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString());

            if (accountId is not null) {
                query = query.Filter("account_id", Constants.Operator.Equals,
                    accountId.Value.ToString());
            }

            var rows = await query
                .Order("started_at", Constants.Ordering.Descending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<bool> HasInProgressForAccountAsync(Guid accountId,
            CancellationToken ct) =>
            await _repository.Query()
                .Filter("account_id", Constants.Operator.Equals, accountId.ToString())
                .Filter("status", Constants.Operator.Equals,
                    nameof(ReconciliationStatus.InProgress))
                .Count(Constants.CountType.Exact, ct) > 0;

        public async Task AddAsync(Reconciliation reconciliation, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(reconciliation), ct);

        public async Task UpdateAsync(Reconciliation reconciliation, CancellationToken ct) {
            var existing = await _repository.GetAsync(reconciliation.Id, ct);
            var model = ToModel(reconciliation);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static Reconciliation ToDomain(ReconciliationModel model) =>
            Reconciliation.Restore(model.Id, model.EntityId, model.AccountId,
                DateOnly.FromDateTime(model.StatementDate), model.StatementBalance,
                Enum.Parse<ReconciliationStatus>(model.Status), model.ClearedLineIds,
                model.ClearedBalance, model.Difference, model.Explanation, model.StartedBy,
                model.StartedAt, model.CompletedBy, model.CompletedAt);

        private static ReconciliationModel ToModel(Reconciliation reconciliation) =>
            new() {
                Id = reconciliation.Id,
                EntityId = reconciliation.EntityId,
                AccountId = reconciliation.AccountId,
                StatementDate = reconciliation.StatementDate.ToDateTime(TimeOnly.MinValue),
                StatementBalance = reconciliation.StatementBalance,
                Status = reconciliation.Status.ToString(),
                ClearedLineIds = reconciliation.ClearedLineIds.ToList(),
                ClearedBalance = reconciliation.ClearedBalance,
                Difference = reconciliation.Difference,
                Explanation = reconciliation.Explanation,
                StartedBy = reconciliation.StartedBy,
                StartedAt = reconciliation.StartedAt,
                CompletedBy = reconciliation.CompletedBy,
                CompletedAt = reconciliation.CompletedAt
            };
    }

    internal sealed class DocumentRepository : IDocumentRepository {
        private readonly SupabaseRepository<AccountingDocumentModel> _repository;

        public DocumentRepository(SupabaseRepository<AccountingDocumentModel> repository) {
            _repository = repository;
        }

        public async Task<AccountingDocument?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<AccountingDocument>> ListAsync(Guid entityId,
            Guid? journalEntryId, Guid? reconciliationId, CancellationToken ct) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString());

            if (journalEntryId is not null) {
                query = query.Filter("journal_entry_id", Constants.Operator.Equals,
                    journalEntryId.Value.ToString());
            }

            if (reconciliationId is not null) {
                query = query.Filter("reconciliation_id", Constants.Operator.Equals,
                    reconciliationId.Value.ToString());
            }

            var rows = await query
                .Order("uploaded_at", Constants.Ordering.Descending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<long> SumSizeBytesAsync(CancellationToken ct) {
            var rows = await _repository.Query().Get(ct);
            return rows.Models.Sum(model => model.SizeBytes);
        }

        public async Task AddAsync(AccountingDocument document, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(document), ct);

        private static AccountingDocument ToDomain(AccountingDocumentModel model) =>
            AccountingDocument.Restore(model.Id, model.EntityId, model.JournalEntryId,
                model.ReconciliationId, model.FileName, model.ContentType, model.SizeBytes,
                model.StoragePath, model.Description, model.UploadedBy, model.UploadedAt);

        private static AccountingDocumentModel ToModel(AccountingDocument document) =>
            new() {
                Id = document.Id,
                EntityId = document.EntityId,
                JournalEntryId = document.JournalEntryId,
                ReconciliationId = document.ReconciliationId,
                FileName = document.FileName,
                ContentType = document.ContentType,
                SizeBytes = document.SizeBytes,
                StoragePath = document.StoragePath,
                Description = document.Description,
                UploadedBy = document.UploadedBy,
                UploadedAt = document.UploadedAt
            };
    }
}
