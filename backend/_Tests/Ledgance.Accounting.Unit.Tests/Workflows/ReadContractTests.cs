using Ledgance.Accounting.Ledger.Application.Published;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class ReadContractTests {
        private readonly InMemoryEntityRepository _entities = new();
        private readonly InMemoryFiscalPeriodRepository _periods = new();
        private readonly InMemoryAccountRepository _accounts = new();
        private readonly InMemoryLedgerLineRepository _ledgerLines = new();

        private AccountingReadContract Contract() =>
            new(_entities, _periods, _accounts, _ledgerLines);

        [Fact]
        public async Task The_trial_balance_snapshot_derives_from_posted_ledger_lines_only() {
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            _entities.Entities.Add(entity);

            var period = FiscalPeriod.Open(entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            _periods.Periods.Add(period);

            var cash = Account.Open(entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            var sales = Account.Open(entity.Id, "4010", "Sales", AccountType.Revenue, "",
                null);
            _accounts.Accounts.AddRange([cash, sales]);

            var entry = JournalEntry.Draft(entity.Id, 1, new DateOnly(2026, 3, 10),
                "Cash sale", "", [
                    JournalLine.Create(cash.Id, "", 750, 0),
                    JournalLine.Create(sales.Id, "", 0, 750)
                ], Guid.NewGuid());
            entry.Post(period, Guid.NewGuid());
            await _ledgerLines.AddRangeAsync(entry.ToLedgerLines(), entity.Id,
                CancellationToken.None);

            // A draft dated in the period must not appear — it has no ledger lines.
            _periods.Periods.Add(FiscalPeriod.Open(entity.Id, "April 2026",
                new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));

            var snapshot = await Contract().GetTrialBalanceAsync(entity.Id, period.Id,
                CancellationToken.None);

            Assert.NotNull(snapshot);
            Assert.Equal(period.EndDate, snapshot!.AsOf);
            Assert.Equal(2, snapshot.Lines.Count);
            Assert.Equal(snapshot.Lines.Sum(line => line.Debit),
                snapshot.Lines.Sum(line => line.Credit));
            Assert.Equal(750m, snapshot.Lines.Single(line => line.AccountCode == "1010").Debit);
        }

        [Fact]
        public async Task A_period_of_another_entity_returns_no_snapshot() {
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            _entities.Entities.Add(entity);

            var foreignPeriod = FiscalPeriod.Open(Guid.NewGuid(), "Foreign",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            _periods.Periods.Add(foreignPeriod);

            var snapshot = await Contract().GetTrialBalanceAsync(entity.Id, foreignPeriod.Id,
                CancellationToken.None);

            Assert.Null(snapshot);
        }

        [Fact]
        public async Task Entities_and_periods_are_exposed_as_snapshots() {
            var entity = AccountingEntity.Create("Acme", "Acme Corp.", "PHP");
            _entities.Entities.Add(entity);
            _periods.Periods.Add(FiscalPeriod.Open(entity.Id, "Q2",
                new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)));
            _periods.Periods.Add(FiscalPeriod.Open(entity.Id, "Q1",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));

            var entities = await Contract().ListEntitiesAsync(CancellationToken.None);
            var periods = await Contract().ListPeriodsAsync(entity.Id, CancellationToken.None);

            Assert.Single(entities);
            Assert.Equal("PHP", entities[0].BaseCurrency);
            Assert.Equal(["Q1", "Q2"], periods.Select(period => period.Name).ToArray());
        }
    }
}
