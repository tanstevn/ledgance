using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Unit.Tests.Domain {
    public class FiscalPeriodAndEntityTests {
        private static readonly Guid EntityId = Guid.NewGuid();

        private static FiscalPeriod March() =>
            FiscalPeriod.Open(EntityId, "March 2026", new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 31));

        [Fact]
        public void A_period_must_end_on_or_after_it_starts() =>
            Assert.Throws<DomainRuleException>(() => FiscalPeriod.Open(EntityId, "Broken",
                new DateOnly(2026, 3, 31), new DateOnly(2026, 3, 1)));

        [Fact]
        public void Overlap_detection_covers_partial_and_full_overlaps() {
            var period = March();

            Assert.True(period.Overlaps(new DateOnly(2026, 3, 15), new DateOnly(2026, 4, 15)));
            Assert.True(period.Overlaps(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)));
            Assert.False(period.Overlaps(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
        }

        [Fact]
        public void A_period_with_draft_entries_cannot_be_closed() {
            var period = March();

            Assert.Throws<DomainRuleException>(
                () => period.Close(hasDraftEntries: true, Guid.NewGuid()));
        }

        [Fact]
        public void Closing_and_reopening_updates_the_close_audit_fields() {
            var period = March();
            var closer = Guid.NewGuid();

            period.Close(hasDraftEntries: false, closer);
            Assert.Equal(FiscalPeriodStatus.Closed, period.Status);
            Assert.Equal(closer, period.ClosedBy);

            Assert.Throws<DomainRuleException>(
                () => period.Close(hasDraftEntries: false, closer));

            period.Reopen();
            Assert.True(period.IsOpen);
            Assert.Null(period.ClosedBy);

            Assert.Throws<DomainRuleException>(() => period.Reopen());
        }

        [Fact]
        public void The_base_currency_must_be_a_three_letter_code() =>
            Assert.Throws<DomainRuleException>(
                () => AccountingEntity.Create("Acme", "", "PESO"));

        [Fact]
        public void An_entity_with_open_periods_cannot_be_archived() {
            var entity = AccountingEntity.Create("Acme", "Acme Corp.", "php");

            Assert.Equal("PHP", entity.BaseCurrency);
            Assert.Throws<DomainRuleException>(() => entity.Archive(hasOpenPeriods: true));

            entity.Archive(hasOpenPeriods: false);
            Assert.True(entity.IsArchived);
            Assert.Throws<DomainRuleException>(() => entity.Update("Renamed", ""));
            Assert.Throws<DomainRuleException>(() => entity.Archive(hasOpenPeriods: false));
        }
    }
}
