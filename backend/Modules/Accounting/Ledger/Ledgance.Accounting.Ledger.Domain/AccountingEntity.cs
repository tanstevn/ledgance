using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Domain {
    /// <summary>
    /// A set of books. One organization may keep books for several entities; every ledger
    /// record belongs to exactly one entity.
    /// </summary>
    public sealed class AccountingEntity {
        private AccountingEntity() { }

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string LegalName { get; private set; } = string.Empty;
        public string BaseCurrency { get; private set; } = string.Empty;
        public bool IsArchived { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public static AccountingEntity Create(string name, string legalName, string baseCurrency) {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (string.IsNullOrWhiteSpace(baseCurrency) || baseCurrency.Trim().Length != 3) {
                throw new DomainRuleException(
                    "The base currency must be a three-letter ISO code.");
            }

            return new AccountingEntity {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                LegalName = legalName.Trim(),
                BaseCurrency = baseCurrency.Trim().ToUpperInvariant(),
                CreatedAt = DateTime.UtcNow
            };
        }

        public static AccountingEntity Restore(Guid id, string name, string legalName,
            string baseCurrency, bool isArchived, DateTime createdAt) =>
            new() {
                Id = id,
                Name = name,
                LegalName = legalName,
                BaseCurrency = baseCurrency,
                IsArchived = isArchived,
                CreatedAt = createdAt
            };

        /// <summary>
        /// The base currency is fixed at creation — changing it under existing postings would
        /// silently restate every balance.
        /// </summary>
        public void Update(string name, string legalName) {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            Name = name.Trim();
            LegalName = legalName.Trim();
        }

        /// <summary>
        /// Entities are never deleted — their books remain readable. Archiving requires every
        /// fiscal period to be closed first.
        /// </summary>
        public void Archive(bool hasOpenPeriods) {
            if (IsArchived) {
                throw new DomainRuleException("This entity is already archived.");
            }

            if (hasOpenPeriods) {
                throw new DomainRuleException(
                    "An entity with open fiscal periods cannot be archived.");
            }

            IsArchived = true;
        }

        public void EnsureActive() {
            if (IsArchived) {
                throw new DomainRuleException("An archived entity cannot be modified.");
            }
        }
    }
}
