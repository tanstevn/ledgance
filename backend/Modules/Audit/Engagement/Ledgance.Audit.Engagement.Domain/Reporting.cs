using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum AuditOpinion { Unqualified, Qualified, Adverse, Disclaimer }

    public sealed class AuditReport {
        private AuditReport() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public AuditOpinion Opinion { get; private set; }
        public string BasisForOpinion { get; private set; } = string.Empty;
        public string KeyAuditMatters { get; private set; } = string.Empty;
        public string OtherInformation { get; private set; } = string.Empty;
        public bool IsFinalized { get; private set; }
        public Guid? FinalizedBy { get; private set; }
        public DateTime? FinalizedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public static AuditReport Draft(Guid engagementId) =>
            new() {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Opinion = AuditOpinion.Unqualified,
                UpdatedAt = DateTime.UtcNow
            };

        public static AuditReport Restore(Guid id, Guid engagementId, AuditOpinion opinion,
            string basisForOpinion, string keyAuditMatters, string otherInformation,
            bool isFinalized, Guid? finalizedBy, DateTime? finalizedAt, DateTime updatedAt) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Opinion = opinion,
                BasisForOpinion = basisForOpinion,
                KeyAuditMatters = keyAuditMatters,
                OtherInformation = otherInformation,
                IsFinalized = isFinalized,
                FinalizedBy = finalizedBy,
                FinalizedAt = finalizedAt,
                UpdatedAt = updatedAt
            };

        public void UpdateDraft(AuditOpinion opinion, string basisForOpinion,
            string keyAuditMatters, string otherInformation) {
            if (IsFinalized) {
                throw new DomainRuleException("A finalized audit report cannot be edited.");
            }

            Opinion = opinion;
            BasisForOpinion = basisForOpinion.Trim();
            KeyAuditMatters = keyAuditMatters.Trim();
            OtherInformation = otherInformation.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void Finalize(Guid userId, EngagementRole? actorTeamRole, int openFindings) {
            if (IsFinalized) {
                throw new DomainRuleException("The audit report is already finalized.");
            }

            if (actorTeamRole != EngagementRole.Partner) {
                throw new DomainRuleException(
                    "Only an engagement partner can finalize the audit report.");
            }

            if (openFindings > 0) {
                throw new DomainRuleException(
                    $"The report cannot be finalized while {openFindings} finding(s) remain open.");
            }

            // A modified opinion must explain itself; an unqualified one may stand alone.
            if (Opinion != AuditOpinion.Unqualified
                && string.IsNullOrWhiteSpace(BasisForOpinion)) {
                throw new DomainRuleException(
                    "A modified opinion requires a documented basis for opinion.");
            }

            IsFinalized = true;
            FinalizedBy = userId;
            FinalizedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public enum TrialBalanceSource { ExternalCsv, Manual, LedganceAccounting }

    public sealed record TrialBalanceLine(
        string AccountCode,
        string AccountName,
        decimal Debit,
        decimal Credit);

    public sealed class TrialBalanceImport {
        private TrialBalanceImport() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public TrialBalanceSource Source { get; private set; }
        public string PeriodLabel { get; private set; } = string.Empty;
        public List<TrialBalanceLine> Lines { get; private set; } = [];
        public decimal TotalDebits { get; private set; }
        public decimal TotalCredits { get; private set; }
        public Guid ImportedBy { get; private set; }
        public DateTime ImportedAt { get; private set; }

        public bool IsBalanced => TotalDebits == TotalCredits;

        public static TrialBalanceImport Create(Guid engagementId, TrialBalanceSource source,
            string periodLabel, IReadOnlyList<TrialBalanceLine> lines, Guid importedBy) {
            if (lines.Count == 0) {
                throw new DomainRuleException("A trial balance import requires at least one line.");
            }

            if (lines.Any(line => string.IsNullOrWhiteSpace(line.AccountCode))) {
                throw new DomainRuleException("Every trial balance line requires an account code.");
            }

            if (lines.Any(line => line.Debit < 0 || line.Credit < 0)) {
                throw new DomainRuleException("Trial balance amounts cannot be negative.");
            }

            return new TrialBalanceImport {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Source = source,
                PeriodLabel = periodLabel.Trim(),
                Lines = lines.ToList(),
                TotalDebits = lines.Sum(line => line.Debit),
                TotalCredits = lines.Sum(line => line.Credit),
                ImportedBy = importedBy,
                ImportedAt = DateTime.UtcNow
            };
        }

        public static TrialBalanceImport Restore(Guid id, Guid engagementId,
            TrialBalanceSource source, string periodLabel, List<TrialBalanceLine> lines,
            decimal totalDebits, decimal totalCredits, Guid importedBy, DateTime importedAt) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Source = source,
                PeriodLabel = periodLabel,
                Lines = lines,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                ImportedBy = importedBy,
                ImportedAt = importedAt
            };
    }
}
