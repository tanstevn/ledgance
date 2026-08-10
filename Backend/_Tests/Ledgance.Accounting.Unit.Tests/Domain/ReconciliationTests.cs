using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Unit.Tests.Domain {
    public class ReconciliationTests {
        private static Reconciliation Start(decimal statementBalance = 1000m) =>
            Reconciliation.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 31),
                statementBalance, Guid.NewGuid());

        [Fact]
        public void Completing_with_a_matching_cleared_balance_records_a_zero_difference() {
            var reconciliation = Start(1000m);

            reconciliation.Complete(1000m, null, Guid.NewGuid());

            Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
            Assert.Equal(0m, reconciliation.Difference);
            Assert.Null(reconciliation.Explanation);
        }

        [Fact]
        public void An_unresolved_difference_requires_an_explanation() {
            var reconciliation = Start(1000m);

            Assert.Throws<DomainRuleException>(
                () => reconciliation.Complete(900m, "  ", Guid.NewGuid()));

            reconciliation.Complete(900m, "Outstanding check #204 not yet presented.",
                Guid.NewGuid());

            Assert.Equal(100m, reconciliation.Difference);
            Assert.NotNull(reconciliation.Explanation);
        }

        [Fact]
        public void A_completed_reconciliation_is_immutable() {
            var reconciliation = Start(1000m);
            reconciliation.Complete(1000m, null, Guid.NewGuid());

            Assert.Throws<DomainRuleException>(
                () => reconciliation.SetClearedLines([Guid.NewGuid()]));
            Assert.Throws<DomainRuleException>(
                () => reconciliation.Complete(1000m, null, Guid.NewGuid()));
            Assert.Throws<DomainRuleException>(() => reconciliation.Cancel());
        }

        [Fact]
        public void Cleared_lines_are_deduplicated() {
            var reconciliation = Start();
            var lineId = Guid.NewGuid();

            reconciliation.SetClearedLines([lineId, lineId, Guid.NewGuid()]);

            Assert.Equal(2, reconciliation.ClearedLineIds.Count);
        }

        [Fact]
        public void A_cancelled_reconciliation_cannot_be_completed() {
            var reconciliation = Start();
            reconciliation.Cancel();

            Assert.Throws<DomainRuleException>(
                () => reconciliation.Complete(0m, null, Guid.NewGuid()));
        }
    }
}
