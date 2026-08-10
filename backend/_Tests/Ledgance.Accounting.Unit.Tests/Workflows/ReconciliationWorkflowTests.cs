using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Reconciliations;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class ReconciliationWorkflowTests {
        private readonly LedgerHarness _harness;
        private readonly AccountingEntity _entity;
        private readonly Account _cash;
        private readonly Guid _deposit;
        private readonly Guid _payment;

        public ReconciliationWorkflowTests() {
            _harness = new LedgerHarness(TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute]));

            _entity = AccountingEntity.Create("Acme", "", "PHP");
            _harness.Entities.Entities.Add(_entity);

            _cash = Account.Open(_entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            _harness.Accounts.Accounts.Add(_cash);

            _deposit = Guid.NewGuid();
            _payment = Guid.NewGuid();

            _harness.LedgerLines.Lines.AddRange([
                (_entity.Id, new PostedLedgerLine(_deposit, 1, new DateOnly(2026, 3, 5),
                    _cash.Id, "Deposit", 1000m, 0m)),
                (_entity.Id, new PostedLedgerLine(_payment, 2, new DateOnly(2026, 3, 20),
                    _cash.Id, "Payment", 0m, 250m)),
                (_entity.Id, new PostedLedgerLine(Guid.NewGuid(), 3,
                    new DateOnly(2026, 4, 2), _cash.Id, "After statement", 400m, 0m))
            ]);
        }

        private async Task<Guid> StartAsync(decimal statementBalance) {
            var started = await _harness.SendAsync(new StartReconciliationCommand {
                EntityId = _entity.Id,
                AccountId = _cash.Id,
                StatementDate = new DateOnly(2026, 3, 31),
                StatementBalance = statementBalance
            });

            Assert.True(started.Successful);
            return started.Data;
        }

        [Fact]
        public async Task A_full_reconciliation_completes_with_zero_difference() {
            var id = await StartAsync(750m);

            var cleared = await _harness.SendAsync(new SetClearedLinesCommand {
                EntityId = _entity.Id,
                ReconciliationId = id,
                ClearedEntryIds = [_deposit, _payment]
            });
            Assert.True(cleared.Successful);

            var detail = await _harness.SendAsync(new GetReconciliationQuery {
                EntityId = _entity.Id,
                ReconciliationId = id
            });
            Assert.Equal(750m, detail.Data!.WorkingClearedBalance);
            Assert.Equal(0m, detail.Data!.WorkingDifference);
            Assert.Equal(2, detail.Data!.Lines.Count);

            var completed = await _harness.SendAsync(new CompleteReconciliationCommand {
                EntityId = _entity.Id,
                ReconciliationId = id
            });
            Assert.True(completed.Successful);

            var reconciliation = _harness.Reconciliations.Reconciliations.Single();
            Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
            Assert.Equal(0m, reconciliation.Difference);
            Assert.Contains(_harness.Activity.Entries,
                entry => entry.Action == "reconciliation.completed");
        }

        [Fact]
        public async Task Completing_with_an_unexplained_difference_is_rejected() {
            var id = await StartAsync(750m);

            await _harness.SendAsync(new SetClearedLinesCommand {
                EntityId = _entity.Id,
                ReconciliationId = id,
                ClearedEntryIds = [_deposit]
            });

            await Assert.ThrowsAsync<DomainRuleException>(
                () => _harness.SendAsync(new CompleteReconciliationCommand {
                    EntityId = _entity.Id,
                    ReconciliationId = id
                }));

            var explained = await _harness.SendAsync(new CompleteReconciliationCommand {
                EntityId = _entity.Id,
                ReconciliationId = id,
                Explanation = "Payment of 250 is still outstanding."
            });

            Assert.True(explained.Successful);
            Assert.Equal(-250m,
                _harness.Reconciliations.Reconciliations.Single().Difference);
        }

        [Fact]
        public async Task Lines_after_the_statement_date_cannot_be_cleared() {
            var id = await StartAsync(750m);
            var lateEntryId = _harness.LedgerLines.Lines
                .Single(item => item.Line.EntryDate == new DateOnly(2026, 4, 2))
                .Line.EntryId;

            var result = await _harness.SendAsync(new SetClearedLinesCommand {
                EntityId = _entity.Id,
                ReconciliationId = id,
                ClearedEntryIds = [lateEntryId]
            });

            Assert.False(result.Successful);
            Assert.Contains("statement date", result.Errors!.Single());
        }

        [Fact]
        public async Task Only_one_reconciliation_may_be_in_progress_per_account() {
            await StartAsync(750m);

            var second = await _harness.SendAsync(new StartReconciliationCommand {
                EntityId = _entity.Id,
                AccountId = _cash.Id,
                StatementDate = new DateOnly(2026, 4, 30),
                StatementBalance = 1150m
            });

            Assert.False(second.Successful);
            Assert.Contains("already in progress", second.Errors!.Single());
        }

        [Fact]
        public async Task A_cancelled_reconciliation_frees_the_account() {
            var id = await StartAsync(750m);

            var cancelled = await _harness.SendAsync(new CancelReconciliationCommand {
                EntityId = _entity.Id,
                ReconciliationId = id
            });
            Assert.True(cancelled.Successful);

            var restarted = await _harness.SendAsync(new StartReconciliationCommand {
                EntityId = _entity.Id,
                AccountId = _cash.Id,
                StatementDate = new DateOnly(2026, 3, 31),
                StatementBalance = 750m
            });
            Assert.True(restarted.Successful);
        }
    }
}
