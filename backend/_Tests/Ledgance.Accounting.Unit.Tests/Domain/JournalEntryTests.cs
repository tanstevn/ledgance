using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Unit.Tests.Domain {
    public class JournalEntryTests {
        private static readonly Guid EntityId = Guid.NewGuid();
        private static readonly Guid CashAccount = Guid.NewGuid();
        private static readonly Guid RevenueAccount = Guid.NewGuid();
        private static readonly DateOnly Date = new(2026, 3, 15);

        private static JournalEntry DraftEntry(decimal amount = 500m) =>
            JournalEntry.Draft(EntityId, 1, Date, "Cash sale", "INV-001", [
                JournalLine.Create(CashAccount, "Cash received", amount, 0),
                JournalLine.Create(RevenueAccount, "Sales revenue", 0, amount)
            ], Guid.NewGuid());

        private static FiscalPeriod OpenPeriod() =>
            FiscalPeriod.Open(EntityId, "March 2026", new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 31));

        [Fact]
        public void A_journal_line_cannot_carry_both_a_debit_and_a_credit() =>
            Assert.Throws<DomainRuleException>(
                () => JournalLine.Create(CashAccount, "bad", 100, 100));

        [Fact]
        public void A_journal_line_cannot_be_empty_on_both_sides() =>
            Assert.Throws<DomainRuleException>(
                () => JournalLine.Create(CashAccount, "bad", 0, 0));

        [Fact]
        public void A_journal_line_rejects_sub_cent_precision() =>
            Assert.Throws<DomainRuleException>(
                () => JournalLine.Create(CashAccount, "bad", 100.123m, 0));

        [Fact]
        public void An_unbalanced_entry_is_rejected() =>
            Assert.Throws<DomainRuleException>(() =>
                JournalEntry.Draft(EntityId, 1, Date, "Unbalanced", "", [
                    JournalLine.Create(CashAccount, "", 500, 0),
                    JournalLine.Create(RevenueAccount, "", 0, 400)
                ], Guid.NewGuid()));

        [Fact]
        public void An_entry_requires_at_least_two_lines() =>
            Assert.Throws<DomainRuleException>(() =>
                JournalEntry.Draft(EntityId, 1, Date, "One-sided", "", [
                    JournalLine.Create(CashAccount, "", 500, 0)
                ], Guid.NewGuid()));

        [Fact]
        public void An_entry_requires_a_memo() =>
            Assert.Throws<DomainRuleException>(() =>
                JournalEntry.Draft(EntityId, 1, Date, "  ", "", [
                    JournalLine.Create(CashAccount, "", 500, 0),
                    JournalLine.Create(RevenueAccount, "", 0, 500)
                ], Guid.NewGuid()));

        [Fact]
        public void Posting_a_balanced_draft_in_an_open_period_succeeds() {
            var entry = DraftEntry();

            entry.Post(OpenPeriod(), Guid.NewGuid());

            Assert.Equal(JournalEntryStatus.Posted, entry.Status);
            Assert.NotNull(entry.PostedAt);

            var lines = entry.ToLedgerLines();
            Assert.Equal(2, lines.Count);
            Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        }

        [Fact]
        public void Posting_into_a_closed_period_is_rejected() {
            var entry = DraftEntry();
            var period = OpenPeriod();
            period.Close(hasDraftEntries: false, Guid.NewGuid());

            Assert.Throws<DomainRuleException>(() => entry.Post(period, Guid.NewGuid()));
        }

        [Fact]
        public void Posting_outside_the_period_dates_is_rejected() {
            var entry = DraftEntry();
            var period = FiscalPeriod.Open(EntityId, "April 2026", new DateOnly(2026, 4, 1),
                new DateOnly(2026, 4, 30));

            Assert.Throws<DomainRuleException>(() => entry.Post(period, Guid.NewGuid()));
        }

        [Fact]
        public void A_posted_entry_cannot_be_edited() {
            var entry = DraftEntry();
            entry.Post(OpenPeriod(), Guid.NewGuid());

            Assert.Throws<DomainRuleException>(() =>
                entry.UpdateDraft(Date, "Changed", "", [
                    JournalLine.Create(CashAccount, "", 100, 0),
                    JournalLine.Create(RevenueAccount, "", 0, 100)
                ]));
        }

        [Fact]
        public void A_posted_entry_cannot_be_posted_twice() {
            var entry = DraftEntry();
            entry.Post(OpenPeriod(), Guid.NewGuid());

            Assert.Throws<DomainRuleException>(() => entry.Post(OpenPeriod(), Guid.NewGuid()));
        }

        [Fact]
        public void Reversing_a_posted_entry_swaps_every_line() {
            var entry = DraftEntry(750m);
            entry.Post(OpenPeriod(), Guid.NewGuid());

            var reversal = entry.Reverse(2, Date, Guid.NewGuid());
            reversal.Post(OpenPeriod(), Guid.NewGuid());
            entry.MarkReversed(reversal.Id);

            Assert.Equal(JournalEntryStatus.Reversed, entry.Status);
            Assert.Equal(reversal.Id, entry.ReversedByEntryId);
            Assert.Equal(entry.Id, reversal.ReversalOfEntryId);

            var cashLine = reversal.Lines.Single(line => line.AccountId == CashAccount);
            Assert.Equal(0, cashLine.Debit);
            Assert.Equal(750m, cashLine.Credit);
        }

        [Fact]
        public void A_draft_cannot_be_reversed() =>
            Assert.Throws<DomainRuleException>(
                () => DraftEntry().Reverse(2, Date, Guid.NewGuid()));

        [Fact]
        public void An_entry_cannot_be_reversed_twice() {
            var entry = DraftEntry();
            entry.Post(OpenPeriod(), Guid.NewGuid());

            var reversal = entry.Reverse(2, Date, Guid.NewGuid());
            entry.MarkReversed(reversal.Id);

            Assert.Throws<DomainRuleException>(() => entry.Reverse(3, Date, Guid.NewGuid()));
        }

        [Fact]
        public void A_draft_has_no_ledger_lines() =>
            Assert.Throws<DomainRuleException>(() => DraftEntry().ToLedgerLines());
    }
}
