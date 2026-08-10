using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum EngagementStatus { Planning, Fieldwork, Review, SignedOff, Completed }

    public enum EngagementType {
        FinancialStatement,
        Internal,
        Compliance,
        Tax,
        LimitedReview,
        Compilation
    }

    /// <summary>
    /// Snapshot of engagement-wide completion state, gathered by the caller so the aggregate can
    /// enforce stage gates without reaching into other repositories.
    /// </summary>
    public readonly record struct EngagementProgress(
        int OpenProcedures,
        int UnapprovedWorkingPapers,
        int OpenReviewNotes,
        int OpenFindings,
        int UnaddressedHighRisks,
        bool ReportFinalized);

    public sealed class Engagement {
        private Engagement() { }

        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public EngagementType Type { get; private set; }
        public EngagementStatus Status { get; private set; }
        public DateOnly PeriodStart { get; private set; }
        public DateOnly PeriodEnd { get; private set; }
        public DateOnly? FiscalYearEnd { get; private set; }
        public decimal BudgetHours { get; private set; }
        public Guid CreatedBy { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public AuditPlan? Plan { get; private set; }
        public Materiality? Materiality { get; private set; }

        public bool IsActive => Status is not EngagementStatus.Completed;

        public static Engagement Create(Guid clientId, string name, EngagementType type,
            DateOnly periodStart, DateOnly periodEnd, DateOnly? fiscalYearEnd,
            decimal budgetHours, Guid createdBy) {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (periodEnd <= periodStart) {
                throw new DomainRuleException("The engagement period must end after it starts.");
            }

            if (budgetHours < 0) {
                throw new DomainRuleException("Budget hours cannot be negative.");
            }

            return new Engagement {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Name = name.Trim(),
                Type = type,
                Status = EngagementStatus.Planning,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                FiscalYearEnd = fiscalYearEnd,
                BudgetHours = budgetHours,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Engagement Restore(Guid id, Guid clientId, string name,
            EngagementType type, EngagementStatus status, DateOnly periodStart,
            DateOnly periodEnd, DateOnly? fiscalYearEnd, decimal budgetHours,
            Guid createdBy, DateTime createdAt, AuditPlan? plan, Materiality? materiality) =>
            new() {
                Id = id,
                ClientId = clientId,
                Name = name,
                Type = type,
                Status = status,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                FiscalYearEnd = fiscalYearEnd,
                BudgetHours = budgetHours,
                CreatedBy = createdBy,
                CreatedAt = createdAt,
                Plan = plan,
                Materiality = materiality
            };

        public void UpdateDetails(string name, EngagementType type, DateOnly periodStart,
            DateOnly periodEnd, DateOnly? fiscalYearEnd, decimal budgetHours) {
            EnsureNotLocked();
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (periodEnd <= periodStart) {
                throw new DomainRuleException("The engagement period must end after it starts.");
            }

            Name = name.Trim();
            Type = type;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            FiscalYearEnd = fiscalYearEnd;
            BudgetHours = budgetHours;
        }

        public void SavePlan(string scope, string objectives, string strategy,
            DateOnly? timelineStart, DateOnly? timelineEnd) {
            EnsureNotLocked();

            // Changing an approved plan withdraws its approval: what fieldwork relies on must be
            // exactly what a manager approved.
            Plan = AuditPlan.Draft(scope, objectives, strategy, timelineStart, timelineEnd);
        }

        public void ApprovePlan(Guid approverUserId) {
            EnsureNotLocked();

            if (Plan is null) {
                throw new DomainRuleException("There is no audit plan to approve.");
            }

            Plan = Plan.Approve(approverUserId);
        }

        public void SetMateriality(Materiality materiality) {
            EnsureNotLocked();
            Materiality = materiality;
        }

        public void StartFieldwork() {
            EnsureStatus(EngagementStatus.Planning, "Fieldwork can only start from planning.");

            if (Plan is null || !Plan.IsApproved) {
                throw new DomainRuleException(
                    "Fieldwork cannot start before the audit plan is approved.");
            }

            if (Materiality is null) {
                throw new DomainRuleException(
                    "Fieldwork cannot start before materiality has been determined.");
            }

            Status = EngagementStatus.Fieldwork;
        }

        public void SubmitForReview(EngagementProgress progress) {
            EnsureStatus(EngagementStatus.Fieldwork,
                "Only an engagement in fieldwork can be submitted for review.");

            if (progress.OpenProcedures > 0) {
                throw new DomainRuleException(
                    $"{progress.OpenProcedures} audit procedure(s) are not yet completed.");
            }

            Status = EngagementStatus.Review;
        }

        public void SignOff(EngagementProgress progress, EngagementRole? actorTeamRole) {
            EnsureStatus(EngagementStatus.Review,
                "Only an engagement under review can be signed off.");

            if (actorTeamRole != EngagementRole.Partner) {
                throw new DomainRuleException(
                    "Only an engagement partner can sign off the engagement.");
            }

            // Procedures may be added during review, so the gate applies here as well.
            if (progress.OpenProcedures > 0) {
                throw new DomainRuleException(
                    $"{progress.OpenProcedures} audit procedure(s) are not yet completed.");
            }

            if (progress.UnapprovedWorkingPapers > 0) {
                throw new DomainRuleException(
                    $"{progress.UnapprovedWorkingPapers} working paper(s) are not yet approved.");
            }

            if (progress.OpenReviewNotes > 0) {
                throw new DomainRuleException(
                    $"{progress.OpenReviewNotes} review note(s) are still open.");
            }

            if (progress.OpenFindings > 0) {
                throw new DomainRuleException(
                    $"{progress.OpenFindings} finding(s) are still open.");
            }

            if (progress.UnaddressedHighRisks > 0) {
                throw new DomainRuleException(
                    $"{progress.UnaddressedHighRisks} high risk(s) have no responsive procedure.");
            }

            Status = EngagementStatus.SignedOff;
        }

        public void Complete(EngagementProgress progress) {
            EnsureStatus(EngagementStatus.SignedOff,
                "Only a signed-off engagement can be completed.");

            if (!progress.ReportFinalized) {
                throw new DomainRuleException(
                    "The engagement cannot be completed before the audit report is finalized.");
            }

            Status = EngagementStatus.Completed;
        }

        private void EnsureStatus(EngagementStatus expected, string message) {
            if (Status != expected) {
                throw new DomainRuleException(message);
            }
        }

        private void EnsureNotLocked() {
            if (Status is EngagementStatus.SignedOff or EngagementStatus.Completed) {
                throw new DomainRuleException(
                    "A signed-off engagement can no longer be modified.");
            }
        }
    }
}
