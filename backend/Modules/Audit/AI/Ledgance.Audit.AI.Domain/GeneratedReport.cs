using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.AI.Domain {
    public enum GeneratedReportStatus { Draft, Accepted, Rejected }

    /// <summary>
    /// One generated section, with the engagement records it was written from. The sources are
    /// what makes the draft checkable: a reviewer can follow each section back to the findings,
    /// risks, procedures and evidence the model was shown.
    /// </summary>
    public sealed record GeneratedReportSection(
        AuditReportSection Section,
        string Heading,
        string Content,
        IReadOnlyList<string> Sources);

    /// <summary>
    /// An AI-generated audit report held as a draft. It carries no authority of its own: it
    /// enters the record as <see cref="GeneratedReportStatus.Draft"/>, only a reviewer with
    /// engagement review authority can accept it, and accepting it still produces nothing more
    /// than input to the audit report the engagement partner finalizes separately. The model
    /// that produced it is recorded so a reviewer knows what wrote what.
    /// </summary>
    public sealed class GeneratedAuditReport {
        private readonly List<GeneratedReportSection> _sections = [];

        private GeneratedAuditReport() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public string Capability { get; private set; } = string.Empty;
        public string ReportScope { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string Provider { get; private set; } = string.Empty;
        public string Model { get; private set; } = string.Empty;
        public GeneratedReportStatus Status { get; private set; }
        public Guid GeneratedBy { get; private set; }
        public DateTime GeneratedAt { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? ReviewNote { get; private set; }

        public IReadOnlyList<GeneratedReportSection> Sections => _sections;

        public bool IsAwaitingReview => Status == GeneratedReportStatus.Draft;

        public static GeneratedAuditReport Draft(Guid engagementId, string capability,
            string reportScope, string title, IEnumerable<GeneratedReportSection> sections,
            string provider, string model, Guid generatedBy) {
            var report = new GeneratedAuditReport {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Capability = capability,
                ReportScope = reportScope,
                Title = title.Trim(),
                Provider = provider,
                Model = model,
                Status = GeneratedReportStatus.Draft,
                GeneratedBy = generatedBy,
                GeneratedAt = DateTime.UtcNow
            };

            report._sections.AddRange(sections);

            if (report._sections.Count == 0) {
                throw new DomainRuleException(
                    "A generated report must contain at least one section.");
            }

            return report;
        }

        public static GeneratedAuditReport Restore(Guid id, Guid engagementId, string capability,
            string reportScope, string title, IEnumerable<GeneratedReportSection> sections,
            string provider, string model, GeneratedReportStatus status, Guid generatedBy,
            DateTime generatedAt, Guid? reviewedBy, DateTime? reviewedAt, string? reviewNote) {
            var report = new GeneratedAuditReport {
                Id = id,
                EngagementId = engagementId,
                Capability = capability,
                ReportScope = reportScope,
                Title = title,
                Provider = provider,
                Model = model,
                Status = status,
                GeneratedBy = generatedBy,
                GeneratedAt = generatedAt,
                ReviewedBy = reviewedBy,
                ReviewedAt = reviewedAt,
                ReviewNote = reviewNote
            };

            report._sections.AddRange(sections);

            return report;
        }

        /// <summary>
        /// Records that a professional reviewed the draft and is willing to work from it.
        /// <paramref name="hasReviewAuthority"/> is decided by the caller's engagement role —
        /// the domain only guarantees that nothing without that authority, and nothing already
        /// reviewed, can be accepted.
        /// </summary>
        public void Accept(Guid reviewerId, bool hasReviewAuthority, string? note) {
            EnsureReviewable(hasReviewAuthority);

            Status = GeneratedReportStatus.Accepted;
            ReviewedBy = reviewerId;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        public void Reject(Guid reviewerId, bool hasReviewAuthority, string note) {
            EnsureReviewable(hasReviewAuthority);

            if (string.IsNullOrWhiteSpace(note)) {
                throw new DomainRuleException(
                    "Rejecting a generated report requires a reason for the record.");
            }

            Status = GeneratedReportStatus.Rejected;
            ReviewedBy = reviewerId;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = note.Trim();
        }

        private void EnsureReviewable(bool hasReviewAuthority) {
            if (Status != GeneratedReportStatus.Draft) {
                throw new DomainRuleException(
                    $"This generated report has already been {Status.ToString().ToLowerInvariant()}.");
            }

            if (!hasReviewAuthority) {
                throw new DomainRuleException(
                    "Only a manager or engagement partner can review an AI-generated report.");
            }
        }
    }
}
