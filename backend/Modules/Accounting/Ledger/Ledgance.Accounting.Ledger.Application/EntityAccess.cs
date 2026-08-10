using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Application {
    /// <summary>
    /// Resolves the accounting entity every ledger operation is scoped to. Organization
    /// isolation is already guaranteed by the repositories; this guard adds existence and,
    /// for mutations, the archived check in one place.
    /// </summary>
    public interface IEntityGuard {
        Task<AccountingEntity> RequireAsync(Guid entityId, CancellationToken ct);
        Task<AccountingEntity> RequireActiveAsync(Guid entityId, CancellationToken ct);
    }

    public sealed class EntityGuard : IEntityGuard {
        private readonly IEntityRepository _entities;

        public EntityGuard(IEntityRepository entities) {
            _entities = entities;
        }

        public async Task<AccountingEntity> RequireAsync(Guid entityId, CancellationToken ct) =>
            await _entities.FindAsync(entityId, ct)
                ?? throw new DomainRuleException("The accounting entity was not found.");

        public async Task<AccountingEntity> RequireActiveAsync(Guid entityId,
            CancellationToken ct) {
            var entity = await RequireAsync(entityId, ct);
            entity.EnsureActive();
            return entity;
        }
    }
}
