using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ledger;
using Ledgance.Accounting.Ledger.Application.Reports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Identity;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class ReportingWorkflowTests {
        private readonly LedgerHarness _harness;
        private readonly AccountingEntity _entity;
        private readonly FiscalPeriod _period;
        private readonly Account _cash;
        private readonly Account _loan;
        private readonly Account _capital;
        private readonly Account _sales;
        private readonly Account _rent;

        public ReportingWorkflowTests() {
            _harness = new LedgerHarness(TestIdentity.User(OrganizationRole.Viewer,
                permissions: [AccountingLedgerPermissions.Read]));

            _entity = AccountingEntity.Create("Acme", "", "PHP");
            _harness.Entities.Entities.Add(_entity);

            _period = FiscalPeriod.Open(_entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            _harness.Periods.Periods.Add(_period);

            _cash = Account.Open(_entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            _loan = Account.Open(_entity.Id, "2010", "Bank loan", AccountType.Liability, "",
                null);
            _capital = Account.Open(_entity.Id, "3010", "Owner capital", AccountType.Equity,
                "", null);
            _sales = Account.Open(_entity.Id, "4010", "Sales", AccountType.Revenue, "", null);
            _rent = Account.Open(_entity.Id, "5010", "Rent expense", AccountType.Expense, "",
                null);
            _harness.Accounts.Accounts.AddRange([_cash, _loan, _capital, _sales, _rent]);

            // Owner invests 10,000; borrows 5,000; earns 2,000 in sales; pays 800 rent.
            Post(1, new DateOnly(2026, 3, 2), (_cash, 10000m, 0m), (_capital, 0m, 10000m));
            Post(2, new DateOnly(2026, 3, 5), (_cash, 5000m, 0m), (_loan, 0m, 5000m));
            Post(3, new DateOnly(2026, 3, 10), (_cash, 2000m, 0m), (_sales, 0m, 2000m));
            Post(4, new DateOnly(2026, 3, 20), (_rent, 800m, 0m), (_cash, 0m, 800m));
        }

        private void Post(long number, DateOnly date,
            params (Account Account, decimal Debit, decimal Credit)[] lines) {
            var entry = JournalEntry.Draft(_entity.Id, number, date, $"Entry {number}", "",
                lines.Select(line => JournalLine.Create(line.Account.Id, "", line.Debit,
                    line.Credit)),
                Guid.NewGuid());

            entry.Post(_period, Guid.NewGuid());
            _harness.Entries.Entries.Add(entry);
            _harness.LedgerLines.AddRangeAsync(entry.ToLedgerLines(), _entity.Id,
                CancellationToken.None).Wait();
        }

        [Fact]
        public async Task The_trial_balance_balances_and_shows_natural_balances() {
            var result = await _harness.SendAsync(new GetTrialBalanceQuery {
                EntityId = _entity.Id,
                PeriodId = _period.Id
            });

            Assert.True(result.Successful);
            var view = result.Data!;

            Assert.True(view.IsBalanced);
            Assert.Equal(view.TotalDebitBalances, view.TotalCreditBalances);

            var cashRow = view.Rows.Single(row => row.AccountId == _cash.Id);
            Assert.Equal(16200m, cashRow.DebitBalance);

            var salesRow = view.Rows.Single(row => row.AccountId == _sales.Id);
            Assert.Equal(2000m, salesRow.CreditBalance);
        }

        [Fact]
        public async Task The_general_ledger_carries_a_running_balance_and_opening_balance() {
            var result = await _harness.SendAsync(new GetGeneralLedgerQuery {
                EntityId = _entity.Id,
                AccountId = _cash.Id,
                From = new DateOnly(2026, 3, 8),
                To = new DateOnly(2026, 3, 31)
            });

            Assert.True(result.Successful);
            var view = result.Data!;

            Assert.Equal(15000m, view.OpeningBalance);
            Assert.Equal(2, view.Lines.Count);
            Assert.Equal(17000m, view.Lines[0].RunningBalance);
            Assert.Equal(16200m, view.Lines[1].RunningBalance);
            Assert.Equal(16200m, view.ClosingBalance);
        }

        [Fact]
        public async Task The_income_statement_reports_net_income_for_the_period() {
            var result = await _harness.SendAsync(new GetIncomeStatementQuery {
                EntityId = _entity.Id,
                PeriodId = _period.Id
            });

            Assert.True(result.Successful);
            var view = result.Data!;

            Assert.Equal(2000m, view.TotalRevenue);
            Assert.Equal(800m, view.TotalExpenses);
            Assert.Equal(1200m, view.NetIncome);
        }

        [Fact]
        public async Task The_balance_sheet_balances_with_current_earnings() {
            var result = await _harness.SendAsync(new GetBalanceSheetQuery {
                EntityId = _entity.Id,
                PeriodId = _period.Id
            });

            Assert.True(result.Successful);
            var view = result.Data!;

            Assert.Equal(16200m, view.TotalAssets);
            Assert.Equal(5000m, view.TotalLiabilities);
            Assert.Equal(10000m, view.TotalEquity);
            Assert.Equal(1200m, view.CurrentEarnings);
            Assert.True(view.IsBalanced);
        }

        [Fact]
        public async Task Reports_reject_a_period_of_another_entity() {
            var otherPeriod = FiscalPeriod.Open(Guid.NewGuid(), "Foreign",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            _harness.Periods.Periods.Add(otherPeriod);

            var result = await _harness.SendAsync(new GetTrialBalanceQuery {
                EntityId = _entity.Id,
                PeriodId = otherPeriod.Id
            });

            Assert.False(result.Successful);
        }
    }
}
