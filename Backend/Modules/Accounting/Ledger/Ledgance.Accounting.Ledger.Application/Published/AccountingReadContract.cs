using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;

namespace Ledgance.Accounting.Ledger.Application.Published {
    public sealed record AccountingEntitySnapshot(
        Guid Id,
        string Name,
        string BaseCurrency,
        bool IsArchived);

    public sealed record FiscalPeriodSnapshot(
        Guid Id,
        Guid EntityId,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        string Status);

    public sealed record TrialBalanceLineSnapshot(
        string AccountCode,
        string AccountName,
        decimal Debit,
        decimal Credit);

    public sealed record TrialBalanceSnapshot(
        Guid EntityId,
        Guid PeriodId,
        string PeriodName,
        DateOnly AsOf,
        IReadOnlyList<TrialBalanceLineSnapshot> Lines);

    /// <summary>
    /// The read-only contract Accounting publishes for cross-context consumption
    /// (module-boundaries §4). It returns snapshots computed from posted ledger lines only —
    /// never domain aggregates, never drafts, never a write path. Callers are expected to
    /// have verified the organization's sharing authorization; organization isolation itself
    /// is still enforced underneath by the repositories. The surface grows only when an
    /// Audit workflow needs more.
    /// </summary>
    public interface IAccountingReadContract {
        Task<IReadOnlyList<AccountingEntitySnapshot>> ListEntitiesAsync(CancellationToken ct);
        Task<IReadOnlyList<FiscalPeriodSnapshot>> ListPeriodsAsync(Guid entityId,
            CancellationToken ct);
        Task<TrialBalanceSnapshot?> GetTrialBalanceAsync(Guid entityId, Guid periodId,
            CancellationToken ct);
    }

    public sealed class AccountingReadContract : IAccountingReadContract {
        private readonly IEntityRepository _entities;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IAccountRepository _accounts;
        private readonly ILedgerLineRepository _ledgerLines;

        public AccountingReadContract(IEntityRepository entities,
            IFiscalPeriodRepository periods, IAccountRepository accounts,
            ILedgerLineRepository ledgerLines) {
            _entities = entities;
            _periods = periods;
            _accounts = accounts;
            _ledgerLines = ledgerLines;
        }

        public async Task<IReadOnlyList<AccountingEntitySnapshot>> ListEntitiesAsync(
            CancellationToken ct) =>
            (await _entities.ListAsync(ct))
                .Select(entity => new AccountingEntitySnapshot(entity.Id, entity.Name,
                    entity.BaseCurrency, entity.IsArchived))
                .ToList();

        public async Task<IReadOnlyList<FiscalPeriodSnapshot>> ListPeriodsAsync(Guid entityId,
            CancellationToken ct) =>
            (await _periods.ListAsync(entityId, ct))
                .OrderBy(period => period.StartDate)
                .Select(period => new FiscalPeriodSnapshot(period.Id, period.EntityId,
                    period.Name, period.StartDate, period.EndDate,
                    period.Status.ToString()))
                .ToList();

        public async Task<TrialBalanceSnapshot?> GetTrialBalanceAsync(Guid entityId,
            Guid periodId, CancellationToken ct) {
            var period = await _periods.FindAsync(periodId, ct);

            if (period is null || period.EntityId != entityId) {
                return null;
            }

            var accounts = (await _accounts.ListAsync(entityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(entityId, null,
                period.EndDate, ct);

            var rows = lines
                .GroupBy(line => line.AccountId)
                .Select(group => {
                    var account = accounts.GetValueOrDefault(group.Key);
                    var net = group.Sum(line => line.Debit) - group.Sum(line => line.Credit);

                    return new TrialBalanceLineSnapshot(
                        account?.Code ?? string.Empty,
                        account?.Name ?? "Unknown account",
                        net > 0 ? net : 0,
                        net < 0 ? -net : 0);
                })
                .Where(row => row.Debit != 0 || row.Credit != 0)
                .OrderBy(row => row.AccountCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new TrialBalanceSnapshot(entityId, period.Id, period.Name, period.EndDate,
                rows);
        }
    }
}
