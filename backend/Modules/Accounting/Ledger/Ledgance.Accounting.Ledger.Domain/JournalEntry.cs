using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Accounting.Ledger.Domain {
    public enum JournalEntryStatus { Draft, Posted, Reversed }

    public sealed record JournalLine {
        private JournalLine(Guid accountId, string description, decimal debit, decimal credit) {
            AccountId = accountId;
            Description = description;
            Debit = debit;
            Credit = credit;
        }

        public Guid AccountId { get; }
        public string Description { get; }
        public decimal Debit { get; }
        public decimal Credit { get; }

        public decimal Amount => Debit > 0 ? Debit : Credit;

        public static JournalLine Create(Guid accountId, string description, decimal debit,
            decimal credit) {
            if (accountId == Guid.Empty) {
                throw new DomainRuleException("Each journal line must reference an account.");
            }

            if (debit < 0 || credit < 0) {
                throw new DomainRuleException("Journal amounts cannot be negative.");
            }

            if ((debit > 0) == (credit > 0)) {
                throw new DomainRuleException(
                    "Each journal line must carry an amount on exactly one side.");
            }

            if (decimal.Round(debit, 2) != debit || decimal.Round(credit, 2) != credit) {
                throw new DomainRuleException(
                    "Journal amounts cannot have more than two decimal places.");
            }

            return new JournalLine(accountId, description.Trim(), debit, credit);
        }
    }

    /// <summary>
    /// A ledger line materialized by posting. Ledger lines are append-only — the general
    /// ledger, trial balance and reports derive from them, and a posted amount only ever
    /// changes by posting a reversing entry.
    /// </summary>
    public sealed record PostedLedgerLine(
        Guid EntryId,
        long EntryNumber,
        DateOnly EntryDate,
        Guid AccountId,
        string Description,
        decimal Debit,
        decimal Credit);

    public sealed class JournalEntry {
        private readonly List<JournalLine> _lines = [];

        private JournalEntry() { }

        public Guid Id { get; private set; }
        public Guid EntityId { get; private set; }
        public long EntryNumber { get; private set; }
        public DateOnly EntryDate { get; private set; }
        public string Memo { get; private set; } = string.Empty;
        public string Reference { get; private set; } = string.Empty;
        public JournalEntryStatus Status { get; private set; }
        public Guid? ReversalOfEntryId { get; private set; }
        public Guid? ReversedByEntryId { get; private set; }
        public Guid CreatedBy { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public Guid? PostedBy { get; private set; }
        public DateTime? PostedAt { get; private set; }

        public IReadOnlyList<JournalLine> Lines => _lines;
        public decimal TotalDebits => _lines.Sum(line => line.Debit);
        public decimal TotalCredits => _lines.Sum(line => line.Credit);

        public static JournalEntry Draft(Guid entityId, long entryNumber, DateOnly entryDate,
            string memo, string reference, IEnumerable<JournalLine> lines, Guid createdBy) {
            var entry = new JournalEntry {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                EntryNumber = entryNumber,
                Status = JournalEntryStatus.Draft,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            entry.Apply(entryDate, memo, reference, lines);
            return entry;
        }

        public static JournalEntry Restore(Guid id, Guid entityId, long entryNumber,
            DateOnly entryDate, string memo, string reference, JournalEntryStatus status,
            IEnumerable<JournalLine> lines, Guid? reversalOfEntryId, Guid? reversedByEntryId,
            Guid createdBy, DateTime createdAt, Guid? postedBy, DateTime? postedAt) {
            var entry = new JournalEntry {
                Id = id,
                EntityId = entityId,
                EntryNumber = entryNumber,
                EntryDate = entryDate,
                Memo = memo,
                Reference = reference,
                Status = status,
                ReversalOfEntryId = reversalOfEntryId,
                ReversedByEntryId = reversedByEntryId,
                CreatedBy = createdBy,
                CreatedAt = createdAt,
                PostedBy = postedBy,
                PostedAt = postedAt
            };

            entry._lines.AddRange(lines);
            return entry;
        }

        public void UpdateDraft(DateOnly entryDate, string memo, string reference,
            IEnumerable<JournalLine> lines) {
            if (Status != JournalEntryStatus.Draft) {
                throw new DomainRuleException("Only a draft journal entry can be edited.");
            }

            Apply(entryDate, memo, reference, lines);
        }

        public void Post(FiscalPeriod period, Guid postedBy) {
            if (Status != JournalEntryStatus.Draft) {
                throw new DomainRuleException("Only a draft journal entry can be posted.");
            }

            if (period.EntityId != EntityId) {
                throw new DomainRuleException(
                    "The posting period belongs to a different entity.");
            }

            if (!period.Contains(EntryDate)) {
                throw new DomainRuleException(
                    "The journal entry date falls outside the posting period.");
            }

            if (!period.IsOpen) {
                throw new DomainRuleException(
                    "Journal entries cannot be posted into a closed period.");
            }

            Status = JournalEntryStatus.Posted;
            PostedBy = postedBy;
            PostedAt = DateTime.UtcNow;
        }

        public IReadOnlyList<PostedLedgerLine> ToLedgerLines() {
            if (Status == JournalEntryStatus.Draft) {
                throw new DomainRuleException(
                    "A draft journal entry has no ledger lines.");
            }

            return _lines
                .Select(line => new PostedLedgerLine(Id, EntryNumber, EntryDate,
                    line.AccountId, line.Description, line.Debit, line.Credit))
                .ToList();
        }

        /// <summary>
        /// Posted entries are immutable; the only correction path is a reversing entry with
        /// every line swapped. The caller posts the reversal through the normal period rules
        /// and then marks this entry reversed.
        /// </summary>
        public JournalEntry Reverse(long reversalNumber, DateOnly reversalDate, Guid createdBy) {
            if (Status != JournalEntryStatus.Posted) {
                throw new DomainRuleException("Only a posted journal entry can be reversed.");
            }

            if (ReversedByEntryId is not null) {
                throw new DomainRuleException("This journal entry has already been reversed.");
            }

            var reversal = Draft(EntityId, reversalNumber, reversalDate,
                $"Reversal of entry #{EntryNumber}: {Memo}", Reference,
                _lines.Select(line => JournalLine.Create(line.AccountId, line.Description,
                    line.Credit, line.Debit)),
                createdBy);

            reversal.ReversalOfEntryId = Id;
            return reversal;
        }

        public void MarkReversed(Guid reversingEntryId) {
            if (Status != JournalEntryStatus.Posted) {
                throw new DomainRuleException("Only a posted journal entry can be reversed.");
            }

            Status = JournalEntryStatus.Reversed;
            ReversedByEntryId = reversingEntryId;
        }

        private void Apply(DateOnly entryDate, string memo, string reference,
            IEnumerable<JournalLine> lines) {
            if (string.IsNullOrWhiteSpace(memo)) {
                throw new DomainRuleException(
                    "A journal entry requires a memo describing the transaction.");
            }

            var applied = lines.ToList();

            if (applied.Count < 2) {
                throw new DomainRuleException("A journal entry requires at least two lines.");
            }

            var debits = applied.Sum(line => line.Debit);
            var credits = applied.Sum(line => line.Credit);

            if (debits != credits) {
                throw new DomainRuleException(
                    "A journal entry must balance — total debits must equal total credits.");
            }

            if (debits == 0) {
                throw new DomainRuleException("A journal entry cannot be for a zero amount.");
            }

            EntryDate = entryDate;
            Memo = memo.Trim();
            Reference = reference.Trim();
            _lines.Clear();
            _lines.AddRange(applied);
        }
    }
}
