using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum RiskRating { Low = 1, Medium = 2, High = 3 }

    public enum RiskLevel { Low, Medium, High }

    public sealed class Risk {
        private Risk() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string Assertions { get; private set; } = string.Empty;
        public RiskRating Likelihood { get; private set; }
        public RiskRating Impact { get; private set; }
        public string PlannedResponse { get; private set; } = string.Empty;

        public RiskLevel Level => LevelOf(Likelihood, Impact);

        public static RiskLevel LevelOf(RiskRating likelihood, RiskRating impact) {
            var score = (int)likelihood * (int)impact;

            return score >= 6
                ? RiskLevel.High
                : score >= 3 ? RiskLevel.Medium : RiskLevel.Low;
        }

        public static Risk Identify(Guid engagementId, string title, string description,
            string assertions, RiskRating likelihood, RiskRating impact, string plannedResponse) {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            return new Risk {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Title = title.Trim(),
                Description = description.Trim(),
                Assertions = assertions.Trim(),
                Likelihood = likelihood,
                Impact = impact,
                PlannedResponse = plannedResponse.Trim()
            };
        }

        public static Risk Restore(Guid id, Guid engagementId, string title, string description,
            string assertions, RiskRating likelihood, RiskRating impact, string plannedResponse) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Title = title,
                Description = description,
                Assertions = assertions,
                Likelihood = likelihood,
                Impact = impact,
                PlannedResponse = plannedResponse
            };

        public void Reassess(string title, string description, string assertions,
            RiskRating likelihood, RiskRating impact, string plannedResponse) {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            Title = title.Trim();
            Description = description.Trim();
            Assertions = assertions.Trim();
            Likelihood = likelihood;
            Impact = impact;
            PlannedResponse = plannedResponse.Trim();
        }
    }

    public enum ProcedureStatus { Planned, InProgress, Completed, NotApplicable }

    public sealed class AuditProcedure {
        private AuditProcedure() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public string Area { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public List<Guid> RiskIds { get; private set; } = [];
        public Guid? AssigneeUserId { get; private set; }
        public ProcedureStatus Status { get; private set; }
        public string? Conclusion { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public bool IsOpen => Status is ProcedureStatus.Planned or ProcedureStatus.InProgress;

        public static AuditProcedure Plan(Guid engagementId, string area, string title,
            string description, IEnumerable<Guid> riskIds, Guid? assigneeUserId) {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            return new AuditProcedure {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Area = area.Trim(),
                Title = title.Trim(),
                Description = description.Trim(),
                RiskIds = riskIds.Distinct().ToList(),
                AssigneeUserId = assigneeUserId,
                Status = ProcedureStatus.Planned
            };
        }

        public static AuditProcedure Restore(Guid id, Guid engagementId, string area,
            string title, string description, List<Guid> riskIds, Guid? assigneeUserId,
            ProcedureStatus status, string? conclusion, DateTime? completedAt) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Area = area,
                Title = title,
                Description = description,
                RiskIds = riskIds,
                AssigneeUserId = assigneeUserId,
                Status = status,
                Conclusion = conclusion,
                CompletedAt = completedAt
            };

        public void Assign(Guid? userId) {
            EnsureOpen();
            AssigneeUserId = userId;
        }

        public void Start() {
            if (Status != ProcedureStatus.Planned) {
                throw new DomainRuleException("Only a planned procedure can be started.");
            }

            Status = ProcedureStatus.InProgress;
        }

        public void Complete(string conclusion) {
            EnsureOpen();

            if (string.IsNullOrWhiteSpace(conclusion)) {
                throw new DomainRuleException(
                    "A procedure cannot be completed without a documented conclusion.");
            }

            Status = ProcedureStatus.Completed;
            Conclusion = conclusion.Trim();
            CompletedAt = DateTime.UtcNow;
        }

        public void MarkNotApplicable(string justification) {
            EnsureOpen();

            if (string.IsNullOrWhiteSpace(justification)) {
                throw new DomainRuleException(
                    "Marking a procedure not applicable requires a justification.");
            }

            Status = ProcedureStatus.NotApplicable;
            Conclusion = justification.Trim();
            CompletedAt = DateTime.UtcNow;
        }

        private void EnsureOpen() {
            if (!IsOpen) {
                throw new DomainRuleException("This procedure has already been concluded.");
            }
        }
    }
}
