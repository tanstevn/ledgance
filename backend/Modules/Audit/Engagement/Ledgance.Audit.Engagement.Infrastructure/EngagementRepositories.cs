using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Constants = Supabase.Postgrest.Constants;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Engagement.Infrastructure {
    internal sealed class EngagementRepository : IEngagementRepository {
        private readonly SupabaseRepository<EngagementModel> _repository;

        public EngagementRepository(SupabaseRepository<EngagementModel> repository) {
            _repository = repository;
        }

        public async Task<DomainEngagement?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<DomainEngagement>> ListAsync(Guid? clientId,
            CancellationToken ct) {
            var query = _repository.Query();

            if (clientId is not null) {
                query = query.Filter("client_id", Constants.Operator.Equals,
                    clientId.Value.ToString());
            }

            var rows = await query.Order("created_at", Constants.Ordering.Descending).Get(ct);
            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<long> CountActiveAsync(CancellationToken ct) =>
            await _repository.Query()
                .Filter("status", Constants.Operator.NotEqual,
                    nameof(EngagementStatus.Completed))
                .Count(Constants.CountType.Exact, ct);

        public async Task AddAsync(DomainEngagement engagement, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(engagement), ct);

        public async Task UpdateAsync(DomainEngagement engagement, CancellationToken ct) {
            var existing = await _repository.GetAsync(engagement.Id, ct);
            var model = ToModel(engagement);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        internal static DomainEngagement ToDomain(EngagementModel model) =>
            DomainEngagement.Restore(
                model.Id,
                model.ClientId,
                model.Name,
                Enum.Parse<EngagementType>(model.Type),
                Enum.Parse<EngagementStatus>(model.Status),
                DateOnly.FromDateTime(model.PeriodStart),
                DateOnly.FromDateTime(model.PeriodEnd),
                model.FiscalYearEnd is null ? null : DateOnly.FromDateTime(model.FiscalYearEnd.Value),
                model.BudgetHours,
                model.CreatedBy,
                model.CreatedAt,
                model.Plan is null ? null : new AuditPlan(
                    model.Plan.Scope, model.Plan.Objectives, model.Plan.Strategy,
                    model.Plan.TimelineStart is null ? null : DateOnly.FromDateTime(model.Plan.TimelineStart.Value),
                    model.Plan.TimelineEnd is null ? null : DateOnly.FromDateTime(model.Plan.TimelineEnd.Value),
                    model.Plan.IsApproved, model.Plan.ApprovedBy, model.Plan.ApprovedAt),
                model.Materiality is null ? null : new Materiality(
                    model.Materiality.OverallAmount, model.Materiality.PerformanceAmount,
                    model.Materiality.ClearlyTrivialThreshold, model.Materiality.Basis,
                    model.Materiality.Rationale));

        internal static EngagementModel ToModel(DomainEngagement engagement) =>
            new() {
                Id = engagement.Id,
                ClientId = engagement.ClientId,
                Name = engagement.Name,
                Type = engagement.Type.ToString(),
                Status = engagement.Status.ToString(),
                PeriodStart = engagement.PeriodStart.ToDateTime(TimeOnly.MinValue),
                PeriodEnd = engagement.PeriodEnd.ToDateTime(TimeOnly.MinValue),
                FiscalYearEnd = engagement.FiscalYearEnd?.ToDateTime(TimeOnly.MinValue),
                BudgetHours = engagement.BudgetHours,
                CreatedBy = engagement.CreatedBy,
                CreatedAt = engagement.CreatedAt,
                Plan = engagement.Plan is null ? null : new PlanDoc {
                    Scope = engagement.Plan.Scope,
                    Objectives = engagement.Plan.Objectives,
                    Strategy = engagement.Plan.Strategy,
                    TimelineStart = engagement.Plan.TimelineStart?.ToDateTime(TimeOnly.MinValue),
                    TimelineEnd = engagement.Plan.TimelineEnd?.ToDateTime(TimeOnly.MinValue),
                    IsApproved = engagement.Plan.IsApproved,
                    ApprovedBy = engagement.Plan.ApprovedBy,
                    ApprovedAt = engagement.Plan.ApprovedAt
                },
                Materiality = engagement.Materiality is null ? null : new MaterialityDoc {
                    OverallAmount = engagement.Materiality.OverallAmount,
                    PerformanceAmount = engagement.Materiality.PerformanceAmount,
                    ClearlyTrivialThreshold = engagement.Materiality.ClearlyTrivialThreshold,
                    Basis = engagement.Materiality.Basis,
                    Rationale = engagement.Materiality.Rationale
                }
            };
    }

    internal sealed class TeamRepository : ITeamRepository {
        private readonly SupabaseRepository<TeamMemberModel> _repository;

        public TeamRepository(SupabaseRepository<TeamMemberModel> repository) {
            _repository = repository;
        }

        public async Task<IReadOnlyList<EngagementTeamMember>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("assigned_at", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<IReadOnlyList<Guid>> ListEngagementIdsForUserAsync(Guid userId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("user_id", Constants.Operator.Equals, userId.ToString())
                .Get(ct);

            return rows.Models.Select(row => row.EngagementId).Distinct().ToList();
        }

        public async Task<EngagementTeamMember?> FindForUserAsync(Guid engagementId,
            Guid userId, CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Filter("user_id", Constants.Operator.Equals, userId.ToString())
                .Limit(1)
                .Get(ct);

            var model = rows.Models.FirstOrDefault();
            return model is null ? null : ToDomain(model);
        }

        public async Task AddAsync(EngagementTeamMember member, CancellationToken ct) =>
            await _repository.InsertAsync(new TeamMemberModel {
                Id = member.Id,
                EngagementId = member.EngagementId,
                UserId = member.UserId,
                Role = member.Role.ToString(),
                AssignedAt = member.AssignedAt
            }, ct);

        public async Task RemoveAsync(Guid memberId, CancellationToken ct) =>
            await _repository.DeleteAsync(memberId, ct);

        private static EngagementTeamMember ToDomain(TeamMemberModel model) =>
            new(model.Id, model.EngagementId, model.UserId,
                Enum.Parse<EngagementRole>(model.Role), model.AssignedAt);
    }

    internal sealed class EngagementProgressReader : IEngagementProgressReader {
        private readonly IProcedureRepository _procedures;
        private readonly IWorkingPaperRepository _papers;
        private readonly IFindingRepository _findings;
        private readonly IRiskRepository _risks;
        private readonly IReportRepository _reports;

        public EngagementProgressReader(IProcedureRepository procedures,
            IWorkingPaperRepository papers, IFindingRepository findings,
            IRiskRepository risks, IReportRepository reports) {
            _procedures = procedures;
            _papers = papers;
            _findings = findings;
            _risks = risks;
            _reports = reports;
        }

        public async Task<EngagementProgress> GetAsync(Guid engagementId, CancellationToken ct) {
            var procedures = await _procedures.ListAsync(engagementId, ct);
            var papers = await _papers.ListAsync(engagementId, ct);
            var findings = await _findings.ListAsync(engagementId, ct);
            var risks = await _risks.ListAsync(engagementId, ct);
            var report = await _reports.FindByEngagementAsync(engagementId, ct);

            var respondedRiskIds = procedures
                .SelectMany(procedure => procedure.RiskIds)
                .ToHashSet();

            return new EngagementProgress(
                OpenProcedures: procedures.Count(procedure => procedure.IsOpen),
                UnapprovedWorkingPapers: papers.Count(paper =>
                    paper.Status != WorkingPaperStatus.Approved),
                OpenReviewNotes: papers.Sum(paper => paper.OpenNoteCount),
                OpenFindings: findings.Count(finding => finding.IsOpen),
                UnaddressedHighRisks: risks.Count(risk =>
                    risk.Level == RiskLevel.High && !respondedRiskIds.Contains(risk.Id)),
                ReportFinalized: report?.IsFinalized ?? false);
        }
    }
}
