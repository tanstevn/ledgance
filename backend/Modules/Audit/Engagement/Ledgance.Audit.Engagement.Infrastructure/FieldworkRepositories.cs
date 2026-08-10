using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Audit.Engagement.Infrastructure {
    internal sealed class RiskRepository : IRiskRepository {
        private readonly SupabaseRepository<RiskModel> _repository;

        public RiskRepository(SupabaseRepository<RiskModel> repository) {
            _repository = repository;
        }

        public async Task<Risk?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<Risk>> ListAsync(Guid engagementId, CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task AddAsync(Risk risk, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(risk), ct);

        public async Task UpdateAsync(Risk risk, CancellationToken ct) {
            var existing = await _repository.GetAsync(risk.Id, ct);
            var model = ToModel(risk);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static Risk ToDomain(RiskModel model) =>
            Risk.Restore(model.Id, model.EngagementId, model.Title, model.Description,
                model.Assertions, (RiskRating)model.Likelihood, (RiskRating)model.Impact,
                model.PlannedResponse);

        private static RiskModel ToModel(Risk risk) =>
            new() {
                Id = risk.Id,
                EngagementId = risk.EngagementId,
                Title = risk.Title,
                Description = risk.Description,
                Assertions = risk.Assertions,
                Likelihood = (int)risk.Likelihood,
                Impact = (int)risk.Impact,
                PlannedResponse = risk.PlannedResponse
            };
    }

    internal sealed class ProcedureRepository : IProcedureRepository {
        private readonly SupabaseRepository<ProcedureModel> _repository;

        public ProcedureRepository(SupabaseRepository<ProcedureModel> repository) {
            _repository = repository;
        }

        public async Task<AuditProcedure?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<AuditProcedure>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task AddAsync(AuditProcedure procedure, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(procedure), ct);

        public async Task UpdateAsync(AuditProcedure procedure, CancellationToken ct) {
            var existing = await _repository.GetAsync(procedure.Id, ct);
            var model = ToModel(procedure);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static AuditProcedure ToDomain(ProcedureModel model) =>
            AuditProcedure.Restore(model.Id, model.EngagementId, model.Area, model.Title,
                model.Description, model.RiskIds, model.AssigneeUserId,
                Enum.Parse<ProcedureStatus>(model.Status), model.Conclusion, model.CompletedAt);

        private static ProcedureModel ToModel(AuditProcedure procedure) =>
            new() {
                Id = procedure.Id,
                EngagementId = procedure.EngagementId,
                Area = procedure.Area,
                Title = procedure.Title,
                Description = procedure.Description,
                RiskIds = procedure.RiskIds,
                AssigneeUserId = procedure.AssigneeUserId,
                Status = procedure.Status.ToString(),
                Conclusion = procedure.Conclusion,
                CompletedAt = procedure.CompletedAt
            };
    }

    internal sealed class WorkingPaperRepository : IWorkingPaperRepository {
        private readonly SupabaseRepository<WorkingPaperModel> _repository;

        public WorkingPaperRepository(SupabaseRepository<WorkingPaperModel> repository) {
            _repository = repository;
        }

        public async Task<WorkingPaper?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<WorkingPaper>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("reference", Constants.Ordering.Ascending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task AddAsync(WorkingPaper paper, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(paper), ct);

        public async Task UpdateAsync(WorkingPaper paper, CancellationToken ct) {
            var existing = await _repository.GetAsync(paper.Id, ct);
            var model = ToModel(paper);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static WorkingPaper ToDomain(WorkingPaperModel model) =>
            WorkingPaper.Restore(model.Id, model.EngagementId, model.Reference, model.Title,
                model.Content, Enum.Parse<WorkingPaperStatus>(model.Status), model.PreparedBy,
                model.PreparedAt, model.ReviewedBy, model.ReviewedAt, model.ApprovedBy,
                model.ApprovedAt,
                model.Notes.Select(note => new ReviewNote(note.Id, note.AuthorUserId,
                    note.Text, note.CreatedAt, note.ResolvedBy, note.ResolvedAt,
                    note.Resolution)).ToList());

        private static WorkingPaperModel ToModel(WorkingPaper paper) =>
            new() {
                Id = paper.Id,
                EngagementId = paper.EngagementId,
                Reference = paper.Reference,
                Title = paper.Title,
                Content = paper.Content,
                Status = paper.Status.ToString(),
                PreparedBy = paper.PreparedBy,
                PreparedAt = paper.PreparedAt,
                ReviewedBy = paper.ReviewedBy,
                ReviewedAt = paper.ReviewedAt,
                ApprovedBy = paper.ApprovedBy,
                ApprovedAt = paper.ApprovedAt,
                Notes = paper.Notes.Select(note => new ReviewNoteDoc {
                    Id = note.Id,
                    AuthorUserId = note.AuthorUserId,
                    Text = note.Text,
                    CreatedAt = note.CreatedAt,
                    ResolvedBy = note.ResolvedBy,
                    ResolvedAt = note.ResolvedAt,
                    Resolution = note.Resolution
                }).ToList()
            };
    }
}
