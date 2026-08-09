using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Client.Domain {
    public sealed class AuditClient {
        private AuditClient() { }

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Industry { get; private set; } = string.Empty;
        public string ContactName { get; private set; } = string.Empty;
        public string ContactEmail { get; private set; } = string.Empty;
        public string ContactPhone { get; private set; } = string.Empty;
        public string? Website { get; private set; }
        public string? Address { get; private set; }
        public bool IsArchived { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public static AuditClient Create(string name, string industry, string contactName,
            string contactEmail, string contactPhone, string? website, string? address) {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return new AuditClient {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Industry = industry.Trim(),
                ContactName = contactName.Trim(),
                ContactEmail = contactEmail.Trim(),
                ContactPhone = contactPhone.Trim(),
                Website = website?.Trim(),
                Address = address?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
        }

        public static AuditClient Restore(Guid id, string name, string industry,
            string contactName, string contactEmail, string contactPhone, string? website,
            string? address, bool isArchived, DateTime createdAt) =>
            new() {
                Id = id,
                Name = name,
                Industry = industry,
                ContactName = contactName,
                ContactEmail = contactEmail,
                ContactPhone = contactPhone,
                Website = website,
                Address = address,
                IsArchived = isArchived,
                CreatedAt = createdAt
            };

        public void Update(string name, string industry, string contactName,
            string contactEmail, string contactPhone, string? website, string? address) {
            if (IsArchived) {
                throw new DomainRuleException("An archived client cannot be modified.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            Name = name.Trim();
            Industry = industry.Trim();
            ContactName = contactName.Trim();
            ContactEmail = contactEmail.Trim();
            ContactPhone = contactPhone.Trim();
            Website = website?.Trim();
            Address = address?.Trim();
        }

        /// <summary>
        /// Clients are never deleted — engagements and their audit trail must stay traceable to
        /// the client they were performed for.
        /// </summary>
        public void Archive(bool hasActiveEngagements) {
            if (IsArchived) {
                throw new DomainRuleException("This client is already archived.");
            }

            if (hasActiveEngagements) {
                throw new DomainRuleException(
                    "A client with active engagements cannot be archived.");
            }

            IsArchived = true;
        }
    }
}
