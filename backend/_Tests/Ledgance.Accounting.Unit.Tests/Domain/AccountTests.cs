using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Unit.Tests.Domain {
    public class AccountTests {
        private static readonly Guid EntityId = Guid.NewGuid();

        [Theory]
        [InlineData(AccountType.Asset, BalanceSide.Debit)]
        [InlineData(AccountType.Expense, BalanceSide.Debit)]
        [InlineData(AccountType.Liability, BalanceSide.Credit)]
        [InlineData(AccountType.Equity, BalanceSide.Credit)]
        [InlineData(AccountType.Revenue, BalanceSide.Credit)]
        public void Normal_balance_follows_the_account_type(AccountType type,
            BalanceSide expected) =>
            Assert.Equal(expected, Account.NormalBalanceOf(type));

        [Fact]
        public void A_sub_account_must_share_its_parents_type() {
            var parent = Account.Open(EntityId, "1000", "Current assets", AccountType.Asset,
                "", null);

            Assert.Throws<DomainRuleException>(() =>
                Account.Open(EntityId, "4000", "Sales", AccountType.Revenue, "", parent));
        }

        [Fact]
        public void A_sub_account_cannot_belong_to_another_entitys_parent() {
            var parent = Account.Open(Guid.NewGuid(), "1000", "Current assets",
                AccountType.Asset, "", null);

            Assert.Throws<DomainRuleException>(() =>
                Account.Open(EntityId, "1010", "Cash", AccountType.Asset, "", parent));
        }

        [Fact]
        public void A_sub_account_cannot_be_opened_under_an_inactive_parent() {
            var parent = Account.Open(EntityId, "1000", "Current assets", AccountType.Asset,
                "", null);
            parent.Deactivate();

            Assert.Throws<DomainRuleException>(() =>
                Account.Open(EntityId, "1010", "Cash", AccountType.Asset, "", parent));
        }

        [Fact]
        public void An_account_with_postings_cannot_change_type() {
            var account = Account.Open(EntityId, "1010", "Cash", AccountType.Asset, "", null);

            Assert.Throws<DomainRuleException>(() => account.Reclassify(
                AccountType.Expense, hasPostings: true, hasChildren: false, parent: null));
        }

        [Fact]
        public void An_account_with_sub_accounts_cannot_change_type() {
            var account = Account.Open(EntityId, "1000", "Current assets", AccountType.Asset,
                "", null);

            Assert.Throws<DomainRuleException>(() => account.Reclassify(
                AccountType.Liability, hasPostings: false, hasChildren: true, parent: null));
        }

        [Fact]
        public void An_unused_account_can_change_type() {
            var account = Account.Open(EntityId, "5010", "Misc", AccountType.Expense, "", null);

            account.Reclassify(AccountType.Asset, hasPostings: false, hasChildren: false,
                parent: null);

            Assert.Equal(AccountType.Asset, account.Type);
            Assert.Equal(BalanceSide.Debit, account.NormalBalance);
        }

        [Fact]
        public void An_inactive_account_cannot_be_modified() {
            var account = Account.Open(EntityId, "1010", "Cash", AccountType.Asset, "", null);
            account.Deactivate();

            Assert.Throws<DomainRuleException>(() => account.Rename("1011", "Petty cash", ""));
            Assert.Throws<DomainRuleException>(() => account.Deactivate());

            account.Reactivate();
            Assert.True(account.IsActive);
        }

        [Fact]
        public void Natural_balance_signs_debits_and_credits_by_type() {
            Assert.Equal(300m, Account.NaturalBalance(AccountType.Asset, 500m, 200m));
            Assert.Equal(300m, Account.NaturalBalance(AccountType.Revenue, 200m, 500m));
            Assert.Equal(-300m, Account.NaturalBalance(AccountType.Liability, 500m, 200m));
        }
    }
}
