using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Domain {
    public enum ReconciliationStatus { InProgress, Completed, Cancelled }

    /// <summary>
    /// Reconciles one account against an external statement: ledger lines up to the statement
    /// date are marked cleared, and completion requires the cleared balance to meet the
    /// statement balance or the difference to be explained.
    /// </summary>
    public sealed class Reconciliation {
        private readonly List<Guid> _clearedLineIds = [];

        private Reconciliation() { }

        public Guid Id { get; private set; }
        public Guid EntityId { get; private set; }
        public Guid AccountId { get; private set; }
        public DateOnly StatementDate { get; private set; }
        public decimal StatementBalance { get; private set; }
        public ReconciliationStatus Status { get; private set; }
        public decimal? ClearedBalance { get; private set; }
        public decimal? Difference { get; private set; }
        public string? Explanation { get; private set; }
        public Guid StartedBy { get; private set; }
        public DateTime StartedAt { get; private set; }
        public Guid? CompletedBy { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public IReadOnlyList<Guid> ClearedLineIds => _clearedLineIds;

        public static Reconciliation Start(Guid entityId, Guid accountId, DateOnly statementDate,
            decimal statementBalance, Guid startedBy) =>
            new() {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                AccountId = accountId,
                StatementDate = statementDate,
                StatementBalance = statementBalance,
                Status = ReconciliationStatus.InProgress,
                StartedBy = startedBy,
                StartedAt = DateTime.UtcNow
            };

        public static Reconciliation Restore(Guid id, Guid entityId, Guid accountId,
            DateOnly statementDate, decimal statementBalance, ReconciliationStatus status,
            IEnumerable<Guid> clearedLineIds, decimal? clearedBalance, decimal? difference,
            string? explanation, Guid startedBy, DateTime startedAt, Guid? completedBy,
            DateTime? completedAt) {
            var reconciliation = new Reconciliation {
                Id = id,
                EntityId = entityId,
                AccountId = accountId,
                StatementDate = statementDate,
                StatementBalance = statementBalance,
                Status = status,
                ClearedBalance = clearedBalance,
                Difference = difference,
                Explanation = explanation,
                StartedBy = startedBy,
                StartedAt = startedAt,
                CompletedBy = completedBy,
                CompletedAt = completedAt
            };

            reconciliation._clearedLineIds.AddRange(clearedLineIds);
            return reconciliation;
        }

        public void SetClearedLines(IEnumerable<Guid> lineIds) {
            EnsureInProgress();

            _clearedLineIds.Clear();
            _clearedLineIds.AddRange(lineIds.Distinct());
        }

        public void Complete(decimal clearedBalance, string? explanation, Guid completedBy) {
            EnsureInProgress();

            var difference = StatementBalance - clearedBalance;

            if (difference != 0 && string.IsNullOrWhiteSpace(explanation)) {
                throw new DomainRuleException(
                    "A reconciliation with an unresolved difference requires a documented explanation.");
            }

            Status = ReconciliationStatus.Completed;
            ClearedBalance = clearedBalance;
            Difference = difference;
            Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
            CompletedBy = completedBy;
            CompletedAt = DateTime.UtcNow;
        }

        public void Cancel() {
            EnsureInProgress();
            Status = ReconciliationStatus.Cancelled;
        }

        private void EnsureInProgress() {
            if (Status != ReconciliationStatus.InProgress) {
                throw new DomainRuleException(
                    "Only an in-progress reconciliation can be modified.");
            }
        }
    }
}
