using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Accounting.Ledger.Infrastructure {
    internal sealed class EntityRepository : IEntityRepository {
        private readonly SupabaseRepository<AccountingEntityModel> _repository;

        public EntityRepository(SupabaseRepository<AccountingEntityModel> repository) {
            _repository = repository;
        }

        public async Task<AccountingEntity?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<AccountingEntity>> ListAsync(CancellationToken ct) {
            var rows = await _repository.Query()
                .Order("name", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<EntityPage> ListPageAsync(int page, int pageSize, string? search,
            CancellationToken ct) {
            var query = _repository.Query();
            var countQuery = _repository.Query();

            if (!string.IsNullOrWhiteSpace(search)) {
                var pattern = $"%{search.Trim()}%";
                query = query.Filter("name", Constants.Operator.ILike, pattern);
                countQuery = countQuery.Filter("name", Constants.Operator.ILike, pattern);
            }

            var from = (page - 1) * pageSize;

            var rows = await query
                .Order("name", Constants.Ordering.Ascending)
                .Range(from, from + pageSize - 1)
                .Get(ct);

            var total = await countQuery.Count(Constants.CountType.Exact, ct);

            return new EntityPage(rows.Models.Select(ToDomain).ToList(), total);
        }

        public async Task<long> CountActiveAsync(CancellationToken ct) =>
            await _repository.Query()
                .Filter("is_archived", Constants.Operator.Equals, "false")
                .Count(Constants.CountType.Exact, ct);

        public async Task AddAsync(AccountingEntity entity, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(entity), ct);

        public async Task UpdateAsync(AccountingEntity entity, CancellationToken ct) {
            var existing = await _repository.GetAsync(entity.Id, ct);
            var model = ToModel(entity);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static AccountingEntity ToDomain(AccountingEntityModel model) =>
            AccountingEntity.Restore(model.Id, model.Name, model.LegalName,
                model.BaseCurrency, model.IsArchived, model.CreatedAt);

        private static AccountingEntityModel ToModel(AccountingEntity entity) =>
            new() {
                Id = entity.Id,
                Name = entity.Name,
                LegalName = entity.LegalName,
                BaseCurrency = entity.BaseCurrency,
                IsArchived = entity.IsArchived,
                CreatedAt = entity.CreatedAt
            };
    }

    internal sealed class AccountRepository : IAccountRepository {
        private readonly SupabaseRepository<AccountModel> _repository;

        public AccountRepository(SupabaseRepository<AccountModel> repository) {
            _repository = repository;
        }

        public async Task<Account?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<Account>> ListAsync(Guid entityId, CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Order("code", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<bool> CodeExistsAsync(Guid entityId, string code,
            Guid? exceptAccountId, CancellationToken ct) {
            var query = _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("code", Constants.Operator.Equals, code);

            if (exceptAccountId is not null) {
                query = query.Filter("id", Constants.Operator.NotEqual,
                    exceptAccountId.Value.ToString());
            }

            return await query.Count(Constants.CountType.Exact, ct) > 0;
        }

        public async Task<bool> HasChildrenAsync(Guid accountId, CancellationToken ct) =>
            await _repository.Query()
                .Filter("parent_account_id", Constants.Operator.Equals, accountId.ToString())
                .Count(Constants.CountType.Exact, ct) > 0;

        public async Task AddAsync(Account account, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(account), ct);

        public async Task UpdateAsync(Account account, CancellationToken ct) {
            var existing = await _repository.GetAsync(account.Id, ct);
            var model = ToModel(account);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static Account ToDomain(AccountModel model) =>
            Account.Restore(model.Id, model.EntityId, model.Code, model.Name,
                Enum.Parse<AccountType>(model.Type), model.Classification,
                model.ParentAccountId, model.IsActive, model.CreatedAt);

        private static AccountModel ToModel(Account account) =>
            new() {
                Id = account.Id,
                EntityId = account.EntityId,
                Code = account.Code,
                Name = account.Name,
                Type = account.Type.ToString(),
                Classification = account.Classification,
                ParentAccountId = account.ParentAccountId,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt
            };
    }

    internal sealed class FiscalPeriodRepository : IFiscalPeriodRepository {
        private readonly SupabaseRepository<FiscalPeriodModel> _repository;

        public FiscalPeriodRepository(SupabaseRepository<FiscalPeriodModel> repository) {
            _repository = repository;
        }

        public async Task<FiscalPeriod?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<FiscalPeriod>> ListAsync(Guid entityId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Order("start_date", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<FiscalPeriod?> FindContainingAsync(Guid entityId, DateOnly date,
            CancellationToken ct) {
            var value = date.ToString("yyyy-MM-dd");

            var rows = await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("start_date", Constants.Operator.LessThanOrEqual, value)
                .Filter("end_date", Constants.Operator.GreaterThanOrEqual, value)
                .Get(ct);

            var model = rows.Models.FirstOrDefault();
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyDictionary<Guid, EntityPeriodCounts>> CountByEntitiesAsync(
            IEnumerable<Guid> entityIds, CancellationToken ct) {
            var ids = entityIds.Distinct().ToList();

            if (ids.Count == 0) {
                return new Dictionary<Guid, EntityPeriodCounts>();
            }

            var rows = await _repository.Query()
                .Filter("entity_id", Constants.Operator.In, ids.Select(id => id.ToString()).ToList())
                .Get(ct);

            return rows.Models
                .GroupBy(period => period.EntityId)
                .ToDictionary(
                    group => group.Key,
                    group => new EntityPeriodCounts(
                        group.Count(period => period.Status == nameof(FiscalPeriodStatus.Open)),
                        group.Count()));
        }

        public async Task<bool> AnyOpenAsync(Guid entityId, CancellationToken ct) =>
            await _repository.Query()
                .Filter("entity_id", Constants.Operator.Equals, entityId.ToString())
                .Filter("status", Constants.Operator.Equals,
                    nameof(FiscalPeriodStatus.Open))
                .Count(Constants.CountType.Exact, ct) > 0;

        public async Task AddAsync(FiscalPeriod period, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(period), ct);

        public async Task UpdateAsync(FiscalPeriod period, CancellationToken ct) {
            var existing = await _repository.GetAsync(period.Id, ct);
            var model = ToModel(period);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static FiscalPeriod ToDomain(FiscalPeriodModel model) =>
            FiscalPeriod.Restore(model.Id, model.EntityId, model.Name,
                DateOnly.FromDateTime(model.StartDate), DateOnly.FromDateTime(model.EndDate),
                Enum.Parse<FiscalPeriodStatus>(model.Status), model.ClosedBy, model.ClosedAt,
                model.CreatedAt);

        private static FiscalPeriodModel ToModel(FiscalPeriod period) =>
            new() {
                Id = period.Id,
                EntityId = period.EntityId,
                Name = period.Name,
                StartDate = period.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = period.EndDate.ToDateTime(TimeOnly.MinValue),
                Status = period.Status.ToString(),
                ClosedBy = period.ClosedBy,
                ClosedAt = period.ClosedAt,
                CreatedAt = period.CreatedAt
            };
    }
}
