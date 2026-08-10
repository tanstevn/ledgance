using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Domain {
    public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Revenue = 4, Expense = 5 }

    public enum BalanceSide { Debit, Credit }

    public sealed class Account {
        private Account() { }

        public Guid Id { get; private set; }
        public Guid EntityId { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public AccountType Type { get; private set; }
        public string Classification { get; private set; } = string.Empty;
        public Guid? ParentAccountId { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public BalanceSide NormalBalance => NormalBalanceOf(Type);

        public static BalanceSide NormalBalanceOf(AccountType type) =>
            type is AccountType.Asset or AccountType.Expense
                ? BalanceSide.Debit
                : BalanceSide.Credit;

        /// <summary>
        /// Signed balance in the account's natural presentation: debits increase debit-normal
        /// accounts, credits increase credit-normal accounts.
        /// </summary>
        public static decimal NaturalBalance(AccountType type, decimal debits, decimal credits) =>
            NormalBalanceOf(type) == BalanceSide.Debit
                ? debits - credits
                : credits - debits;

        public static Account Open(Guid entityId, string code, string name, AccountType type,
            string classification, Account? parent) {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (parent is not null) {
                if (parent.EntityId != entityId) {
                    throw new DomainRuleException(
                        "The parent account belongs to a different entity.");
                }

                if (parent.Type != type) {
                    throw new DomainRuleException(
                        "A sub-account must share its parent's account type.");
                }

                if (!parent.IsActive) {
                    throw new DomainRuleException(
                        "A sub-account cannot be opened under an inactive account.");
                }
            }

            return new Account {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Code = code.Trim(),
                Name = name.Trim(),
                Type = type,
                Classification = classification.Trim(),
                ParentAccountId = parent?.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Account Restore(Guid id, Guid entityId, string code, string name,
            AccountType type, string classification, Guid? parentAccountId, bool isActive,
            DateTime createdAt) =>
            new() {
                Id = id,
                EntityId = entityId,
                Code = code,
                Name = name,
                Type = type,
                Classification = classification,
                ParentAccountId = parentAccountId,
                IsActive = isActive,
                CreatedAt = createdAt
            };

        public void Rename(string code, string name, string classification) {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            Code = code.Trim();
            Name = name.Trim();
            Classification = classification.Trim();
        }

        /// <summary>
        /// Changing the type of an account that already carries postings or sub-accounts would
        /// restate historical statements, so both block the change.
        /// </summary>
        public void Reclassify(AccountType newType, bool hasPostings, bool hasChildren,
            Account? parent) {
            EnsureActive();

            if (newType == Type) {
                return;
            }

            if (hasPostings) {
                throw new DomainRuleException(
                    "An account with postings cannot change type — open a new account instead.");
            }

            if (hasChildren) {
                throw new DomainRuleException(
                    "An account with sub-accounts cannot change type.");
            }

            if (parent is not null && parent.Type != newType) {
                throw new DomainRuleException(
                    "A sub-account must share its parent's account type.");
            }

            Type = newType;
        }

        /// <summary>
        /// Accounts with history are never deleted; deactivation keeps the history readable
        /// while rejecting new postings.
        /// </summary>
        public void Deactivate() {
            EnsureActive();
            IsActive = false;
        }

        public void Reactivate() {
            if (IsActive) {
                throw new DomainRuleException("This account is already active.");
            }

            IsActive = true;
        }

        private void EnsureActive() {
            if (!IsActive) {
                throw new DomainRuleException("An inactive account cannot be modified.");
            }
        }
    }
}
