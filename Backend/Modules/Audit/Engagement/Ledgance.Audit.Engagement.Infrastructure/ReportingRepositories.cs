using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Audit.Engagement.Infrastructure {
    internal sealed class EvidenceRepository : IEvidenceRepository {
        private readonly SupabaseRepository<EvidenceModel> _repository;

        public EvidenceRepository(SupabaseRepository<EvidenceModel> repository) {
            _repository = repository;
        }

        public async Task<Evidence?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<Evidence>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("uploaded_at", Constants.Ordering.Descending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<long> SumSizeBytesAsync(CancellationToken ct) {
            var rows = await _repository.Query().Get(ct);
            return rows.Models.Sum(model => model.SizeBytes);
        }

        public async Task AddAsync(Evidence evidence, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(evidence), ct);

        public async Task UpdateAsync(Evidence evidence, CancellationToken ct) {
            var existing = await _repository.GetAsync(evidence.Id, ct);
            var model = ToModel(evidence);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static Evidence ToDomain(EvidenceModel model) =>
            Evidence.Restore(model.Id, model.EngagementId, model.WorkingPaperId,
                model.ProcedureId, model.FileName, model.ContentType, model.SizeBytes,
                model.StoragePath, model.Version, model.Description, model.UploadedBy,
                model.UploadedAt);

        private static EvidenceModel ToModel(Evidence evidence) =>
            new() {
                Id = evidence.Id,
                EngagementId = evidence.EngagementId,
                WorkingPaperId = evidence.WorkingPaperId,
                ProcedureId = evidence.ProcedureId,
                FileName = evidence.FileName,
                ContentType = evidence.ContentType,
                SizeBytes = evidence.SizeBytes,
                StoragePath = evidence.StoragePath,
                Version = evidence.Version,
                Description = evidence.Description,
                UploadedBy = evidence.UploadedBy,
                UploadedAt = evidence.UploadedAt
            };
    }

    internal sealed class FindingRepository : IFindingRepository {
        private readonly SupabaseRepository<FindingModel> _repository;

        public FindingRepository(SupabaseRepository<FindingModel> repository) {
            _repository = repository;
        }

        public async Task<Finding?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<Finding>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("raised_at", Constants.Ordering.Descending)
                .Get(ct);

            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task AddAsync(Finding finding, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(finding), ct);

        public async Task UpdateAsync(Finding finding, CancellationToken ct) {
            var existing = await _repository.GetAsync(finding.Id, ct);
            var model = ToModel(finding);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static Finding ToDomain(FindingModel model) =>
            Finding.Restore(model.Id, model.EngagementId, model.Title, model.Description,
                Enum.Parse<FindingSeverity>(model.Severity),
                Enum.Parse<FindingStatus>(model.Status), model.Recommendation,
                model.Resolution, model.EvidenceIds, model.RaisedBy, model.RaisedAt);

        private static FindingModel ToModel(Finding finding) =>
            new() {
                Id = finding.Id,
                EngagementId = finding.EngagementId,
                Title = finding.Title,
                Description = finding.Description,
                Severity = finding.Severity.ToString(),
                Status = finding.Status.ToString(),
                Recommendation = finding.Recommendation,
                Resolution = finding.Resolution,
                EvidenceIds = finding.EvidenceIds,
                RaisedBy = finding.RaisedBy,
                RaisedAt = finding.RaisedAt
            };
    }

    internal sealed class ReportRepository : IReportRepository {
        private readonly SupabaseRepository<ReportModel> _repository;

        public ReportRepository(SupabaseRepository<ReportModel> repository) {
            _repository = repository;
        }

        public async Task<AuditReport?> FindByEngagementAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Limit(1)
                .Get(ct);

            var model = rows.Models.FirstOrDefault();
            return model is null ? null : ToDomain(model);
        }

        public async Task UpsertAsync(AuditReport report, CancellationToken ct) {
            var existing = await _repository.FindAsync(report.Id, ct);
            var model = ToModel(report);

            if (existing is null) {
                await _repository.InsertAsync(model, ct);
            }
            else {
                model.OrganizationId = existing.OrganizationId;
                await _repository.UpdateAsync(model, ct);
            }
        }

        private static AuditReport ToDomain(ReportModel model) =>
            AuditReport.Restore(model.Id, model.EngagementId,
                Enum.Parse<AuditOpinion>(model.Opinion), model.BasisForOpinion,
                model.KeyAuditMatters, model.OtherInformation, model.IsFinalized,
                model.FinalizedBy, model.FinalizedAt, model.UpdatedAt);

        private static ReportModel ToModel(AuditReport report) =>
            new() {
                Id = report.Id,
                EngagementId = report.EngagementId,
                Opinion = report.Opinion.ToString(),
                BasisForOpinion = report.BasisForOpinion,
                KeyAuditMatters = report.KeyAuditMatters,
                OtherInformation = report.OtherInformation,
                IsFinalized = report.IsFinalized,
                FinalizedBy = report.FinalizedBy,
                FinalizedAt = report.FinalizedAt,
                UpdatedAt = report.UpdatedAt
            };
    }

    internal sealed class TrialBalanceRepository : ITrialBalanceRepository {
        private readonly SupabaseRepository<TrialBalanceModel> _repository;

        public TrialBalanceRepository(SupabaseRepository<TrialBalanceModel> repository) {
            _repository = repository;
        }

        public async Task<TrialBalanceImport?> FindLatestAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("imported_at", Constants.Ordering.Descending)
                .Limit(1)
                .Get(ct);

            var model = rows.Models.FirstOrDefault();

            return model is null
                ? null
                : TrialBalanceImport.Restore(model.Id, model.EngagementId,
                    Enum.Parse<TrialBalanceSource>(model.Source), model.PeriodLabel,
                    model.Lines.Select(line => new TrialBalanceLine(line.AccountCode,
                        line.AccountName, line.Debit, line.Credit)).ToList(),
                    model.TotalDebits, model.TotalCredits, model.ImportedBy, model.ImportedAt);
        }

        public async Task AddAsync(TrialBalanceImport import, CancellationToken ct) =>
            await _repository.InsertAsync(new TrialBalanceModel {
                Id = import.Id,
                EngagementId = import.EngagementId,
                Source = import.Source.ToString(),
                PeriodLabel = import.PeriodLabel,
                Lines = import.Lines.Select(line => new TrialBalanceLineDoc {
                    AccountCode = line.AccountCode,
                    AccountName = line.AccountName,
                    Debit = line.Debit,
                    Credit = line.Credit
                }).ToList(),
                TotalDebits = import.TotalDebits,
                TotalCredits = import.TotalCredits,
                ImportedBy = import.ImportedBy,
                ImportedAt = import.ImportedAt
            }, ct);
    }
}
