using Ledgance.Audit.AI.Domain;
using Ledgance.Shared.Application.Ai;
using System.Text;
using System.Text.Json;

namespace Ledgance.Audit.AI.Application.Reporting {
    /// <summary>
    /// Asks the model for its sections as JSON so a generated report arrives as structured,
    /// individually reviewable and individually regenerable sections rather than one wall of
    /// prose. A model that answers with prose anyway is not treated as a failure — the whole
    /// answer becomes a single section, which stays reviewable.
    /// </summary>
    internal static class ReportComposition {
        private static readonly JsonSerializerOptions Options = new() {
            PropertyNameCaseInsensitive = true
        };

        public static string FormatInstruction(IReadOnlyList<AuditReportSection> sections) {
            var builder = new StringBuilder();

            builder.AppendLine("Return only a JSON object of this exact shape, with no prose " +
                "outside it and no markdown fence:");
            builder.AppendLine(
                """{"sections":[{"section":"ExecutiveSummary","heading":"...","content":"...","sources":["..."]}]}""");
            builder.AppendLine();
            builder.AppendLine("Produce exactly these sections, in this order, using these " +
                "values for \"section\": " +
                string.Join(", ", sections.Select(section => section.ToString())) + ".");
            builder.AppendLine("\"content\" is the section text. \"sources\" lists the " +
                "engagement records that section rests on, each named the way the context " +
                "names it (for example \"Finding: Unreconciled bank balance\" or " +
                "\"Risk: Revenue cut-off\"). Leave \"sources\" empty when a section rests on " +
                "nothing in the record — never name a record that was not provided.");

            return builder.ToString();
        }

        public static List<GeneratedReportSection> Parse(string content,
            IReadOnlyList<AuditReportSection> requested) {
            var json = ExtractJson(content);

            if (json is not null) {
                try {
                    var parsed = JsonSerializer.Deserialize<SectionEnvelope>(json, Options);

                    if (parsed?.Sections is { Count: > 0 }) {
                        return [.. parsed.Sections.Select(section => ToDomain(section, requested))];
                    }
                }
                catch (JsonException) {
                    // The model ignored the format; the prose fallback below still yields a
                    // reviewable draft, which is better than failing the generation.
                }
            }

            return [new GeneratedReportSection(
                requested.FirstOrDefault(AuditReportSection.ExecutiveSummary),
                "Generated report", content.Trim(), [])];
        }

        private static GeneratedReportSection ToDomain(SectionDto dto,
            IReadOnlyList<AuditReportSection> requested) =>
            new(Enum.TryParse<AuditReportSection>(dto.Section, ignoreCase: true, out var section)
                    ? section
                    : requested.FirstOrDefault(AuditReportSection.ExecutiveSummary),
                string.IsNullOrWhiteSpace(dto.Heading) ? dto.Section : dto.Heading.Trim(),
                dto.Content.Trim(),
                dto.Sources ?? []);

        private static string? ExtractJson(string content) {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');

            return start >= 0 && end > start
                ? content[start..(end + 1)]
                : null;
        }

        private sealed class SectionEnvelope {
            public List<SectionDto>? Sections { get; set; }
        }

        private sealed class SectionDto {
            public string Section { get; set; } = string.Empty;
            public string Heading { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public List<string>? Sources { get; set; }
        }
    }

    /// <summary>
    /// A generated report as the API returns it. The disclaimer travels with the payload rather
    /// than being left to the client to remember.
    /// </summary>
    public class GeneratedReportView {
        public Guid Id { get; set; }
        public Guid EngagementId { get; set; }
        public string Capability { get; set; } = string.Empty;
        public string ReportScope { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public Guid GeneratedBy { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public List<GeneratedReportSectionView> Sections { get; set; } = [];

        public string Disclaimer { get; set; } =
            "AI-generated draft. It is not an audit report and carries no audit opinion. " +
            "AI output can be wrong or incomplete — a qualified auditor must review every " +
            "section against the engagement record, and the engagement partner remains " +
            "responsible for the report that is issued.";

        public static GeneratedReportView From(GeneratedAuditReport report) =>
            new() {
                Id = report.Id,
                EngagementId = report.EngagementId,
                Capability = report.Capability,
                ReportScope = report.ReportScope,
                Title = report.Title,
                Status = report.Status.ToString(),
                Provider = report.Provider,
                Model = report.Model,
                GeneratedBy = report.GeneratedBy,
                GeneratedAt = report.GeneratedAt,
                ReviewedBy = report.ReviewedBy,
                ReviewedAt = report.ReviewedAt,
                ReviewNote = report.ReviewNote,
                Sections = [.. report.Sections.Select(section =>
                    new GeneratedReportSectionView {
                        Section = section.Section.ToString(),
                        Heading = section.Heading,
                        Content = section.Content,
                        Sources = [.. section.Sources]
                    })]
            };
    }

    public class GeneratedReportSectionView {
        public string Section { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = [];
    }

    internal static class ReportSectionSets {
        /// <summary>Micro-Growth: the sections that make up a complete draft audit report.</summary>
        public static readonly AuditReportSection[] FullDraft = [
            AuditReportSection.ExecutiveSummary,
            AuditReportSection.Scope,
            AuditReportSection.RiskAssessment,
            AuditReportSection.ProceduresPerformed,
            AuditReportSection.EvidenceSummary,
            AuditReportSection.Findings,
            AuditReportSection.Recommendations,
            AuditReportSection.BasisForOpinion,
            AuditReportSection.KeyAuditMatters
        ];

        /// <summary>Small: the full draft plus the material a reviewer and management need.</summary>
        public static readonly AuditReportSection[] Engagement = [
            AuditReportSection.ExecutiveSummary,
            AuditReportSection.ManagementSummary,
            AuditReportSection.Scope,
            AuditReportSection.Approach,
            AuditReportSection.Materiality,
            AuditReportSection.RiskAssessment,
            AuditReportSection.ProceduresPerformed,
            AuditReportSection.EvidenceSummary,
            AuditReportSection.Findings,
            AuditReportSection.Recommendations,
            AuditReportSection.BasisForOpinion,
            AuditReportSection.KeyAuditMatters,
            AuditReportSection.Conclusion
        ];

        /// <summary>Medium: what a client or firm-level report across engagements covers.</summary>
        public static readonly AuditReportSection[] Portfolio = [
            AuditReportSection.ExecutiveSummary,
            AuditReportSection.Scope,
            AuditReportSection.RiskAssessment,
            AuditReportSection.Findings,
            AuditReportSection.Recommendations,
            AuditReportSection.Conclusion
        ];
    }
}
