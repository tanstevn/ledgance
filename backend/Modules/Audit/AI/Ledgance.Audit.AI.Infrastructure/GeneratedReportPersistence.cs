using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Audit.AI.Infrastructure {
    [Table("audit_generated_reports")]
    public class GeneratedReportModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("capability")]
        public string Capability { get; set; } = string.Empty;

        [Column("report_scope")]
        public string ReportScope { get; set; } = string.Empty;

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("provider")]
        public string Provider { get; set; } = string.Empty;

        [Column("model")]
        public string Model { get; set; } = string.Empty;

        [Column("sections")]
        public List<GeneratedReportSectionDoc> Sections { get; set; } = [];

        [Column("generated_by")]
        public Guid GeneratedBy { get; set; }

        [Column("generated_at")]
        public DateTime GeneratedAt { get; set; }

        [Column("reviewed_by")]
        public Guid? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        [Column("review_note")]
        public string? ReviewNote { get; set; }
    }

    public class GeneratedReportSectionDoc {
        [JsonProperty("section")] public string Section { get; set; } = string.Empty;
        [JsonProperty("heading")] public string Heading { get; set; } = string.Empty;
        [JsonProperty("content")] public string Content { get; set; } = string.Empty;
        [JsonProperty("sources")] public List<string> Sources { get; set; } = [];
    }

    internal sealed class GeneratedReportRepository : IGeneratedReportRepository {
        private readonly SupabaseRepository<GeneratedReportModel> _repository;

        public GeneratedReportRepository(SupabaseRepository<GeneratedReportModel> repository) {
            _repository = repository;
        }

        public async Task<GeneratedAuditReport?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<GeneratedAuditReport>> ListAsync(Guid engagementId,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("engagement_id", Constants.Operator.Equals, engagementId.ToString())
                .Order("generated_at", Constants.Ordering.Descending)
                .Get(ct);

            return [.. rows.Models.Select(ToDomain)];
        }

        public async Task AddAsync(GeneratedAuditReport report, CancellationToken ct) =>
            await _repository.InsertAsync(ToModel(report), ct);

        public async Task UpdateAsync(GeneratedAuditReport report, CancellationToken ct) {
            var existing = await _repository.GetAsync(report.Id, ct);
            var model = ToModel(report);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static GeneratedAuditReport ToDomain(GeneratedReportModel model) =>
            GeneratedAuditReport.Restore(model.Id, model.EngagementId, model.Capability,
                model.ReportScope, model.Title,
                model.Sections.Select(section => new GeneratedReportSection(
                    Enum.TryParse<AuditReportSection>(section.Section, out var parsed)
                        ? parsed
                        : AuditReportSection.ExecutiveSummary,
                    section.Heading, section.Content, section.Sources)),
                model.Provider, model.Model,
                Enum.TryParse<GeneratedReportStatus>(model.Status, out var status)
                    ? status
                    : GeneratedReportStatus.Draft,
                model.GeneratedBy, model.GeneratedAt, model.ReviewedBy, model.ReviewedAt,
                model.ReviewNote);

        private static GeneratedReportModel ToModel(GeneratedAuditReport report) =>
            new() {
                Id = report.Id,
                EngagementId = report.EngagementId,
                Capability = report.Capability,
                ReportScope = report.ReportScope,
                Title = report.Title,
                Status = report.Status.ToString(),
                Provider = report.Provider,
                Model = report.Model,
                Sections = [.. report.Sections.Select(section => new GeneratedReportSectionDoc {
                    Section = section.Section.ToString(),
                    Heading = section.Heading,
                    Content = section.Content,
                    Sources = [.. section.Sources]
                })],
                GeneratedBy = report.GeneratedBy,
                GeneratedAt = report.GeneratedAt,
                ReviewedBy = report.ReviewedBy,
                ReviewedAt = report.ReviewedAt,
                ReviewNote = report.ReviewNote
            };
    }
}
