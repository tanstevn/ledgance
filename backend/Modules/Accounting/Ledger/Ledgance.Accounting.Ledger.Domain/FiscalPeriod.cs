using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Domain {
    public enum FiscalPeriodStatus { Open, Closed }

    public sealed class FiscalPeriod {
        private FiscalPeriod() { }

        public Guid Id { get; private set; }
        public Guid EntityId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public FiscalPeriodStatus Status { get; private set; }
        public Guid? ClosedBy { get; private set; }
        public DateTime? ClosedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public bool IsOpen => Status == FiscalPeriodStatus.Open;

        public static FiscalPeriod Open(Guid entityId, string name, DateOnly startDate,
            DateOnly endDate) {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (endDate < startDate) {
                throw new DomainRuleException("A fiscal period must end on or after it starts.");
            }

            return new FiscalPeriod {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Name = name.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                Status = FiscalPeriodStatus.Open,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static FiscalPeriod Restore(Guid id, Guid entityId, string name,
            DateOnly startDate, DateOnly endDate, FiscalPeriodStatus status, Guid? closedBy,
            DateTime? closedAt, DateTime createdAt) =>
            new() {
                Id = id,
                EntityId = entityId,
                Name = name,
                StartDate = startDate,
                EndDate = endDate,
                Status = status,
                ClosedBy = closedBy,
                ClosedAt = closedAt,
                CreatedAt = createdAt
            };

        public bool Contains(DateOnly date) =>
            date >= StartDate && date <= EndDate;

        public bool Overlaps(DateOnly startDate, DateOnly endDate) =>
            startDate <= EndDate && endDate >= StartDate;

        public void Close(bool hasDraftEntries, Guid closedBy) {
            if (!IsOpen) {
                throw new DomainRuleException("This fiscal period is already closed.");
            }

            if (hasDraftEntries) {
                throw new DomainRuleException(
                    "A period with draft journal entries cannot be closed — post or delete the drafts first.");
            }

            Status = FiscalPeriodStatus.Closed;
            ClosedBy = closedBy;
            ClosedAt = DateTime.UtcNow;
        }

        public void Reopen() {
            if (IsOpen) {
                throw new DomainRuleException("This fiscal period is already open.");
            }

            Status = FiscalPeriodStatus.Open;
            ClosedBy = null;
            ClosedAt = null;
        }
    }
}
