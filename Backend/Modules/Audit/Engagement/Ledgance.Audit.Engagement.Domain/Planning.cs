using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public sealed record AuditPlan(
        string Scope,
        string Objectives,
        string Strategy,
        DateOnly? TimelineStart,
        DateOnly? TimelineEnd,
        bool IsApproved,
        Guid? ApprovedBy,
        DateTime? ApprovedAt) {
        public static AuditPlan Draft(string scope, string objectives, string strategy,
            DateOnly? timelineStart, DateOnly? timelineEnd) {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);
            ArgumentException.ThrowIfNullOrWhiteSpace(objectives);

            if (timelineStart is not null && timelineEnd is not null
                && timelineEnd < timelineStart) {
                throw new DomainRuleException("The plan timeline must end after it starts.");
            }

            return new AuditPlan(scope.Trim(), objectives.Trim(), strategy.Trim(),
                timelineStart, timelineEnd, false, null, null);
        }

        public AuditPlan Approve(Guid approverUserId) {
            if (IsApproved) {
                throw new DomainRuleException("The audit plan is already approved.");
            }

            return this with {
                IsApproved = true,
                ApprovedBy = approverUserId,
                ApprovedAt = DateTime.UtcNow
            };
        }
    }

    public sealed record Materiality(
        decimal OverallAmount,
        decimal PerformanceAmount,
        decimal ClearlyTrivialThreshold,
        string Basis,
        string Rationale) {
        public static Materiality Create(decimal overallAmount, decimal performanceAmount,
            decimal clearlyTrivialThreshold, string basis, string rationale) {
            if (overallAmount <= 0) {
                throw new DomainRuleException("Overall materiality must be greater than zero.");
            }

            if (performanceAmount <= 0 || performanceAmount >= overallAmount) {
                throw new DomainRuleException(
                    "Performance materiality must be positive and below overall materiality.");
            }

            if (clearlyTrivialThreshold <= 0 || clearlyTrivialThreshold >= performanceAmount) {
                throw new DomainRuleException(
                    "The clearly-trivial threshold must be positive and below performance materiality.");
            }

            if (string.IsNullOrWhiteSpace(basis) || string.IsNullOrWhiteSpace(rationale)) {
                throw new DomainRuleException(
                    "Materiality requires a benchmark basis and a documented rationale.");
            }

            return new Materiality(overallAmount, performanceAmount, clearlyTrivialThreshold,
                basis.Trim(), rationale.Trim());
        }
    }
}
