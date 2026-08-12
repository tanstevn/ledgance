using Ledgance.Audit.AI.Domain;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.Unit.Tests.Domain {
    public class GeneratedReportTests {
        private static GeneratedAuditReport Draft() =>
            GeneratedAuditReport.Draft(Guid.NewGuid(), AuditAiCapabilities.ReportDraft.Key,
                AiReportScopes.FullDraft, "Draft audit report",
                [new GeneratedReportSection(AuditReportSection.ExecutiveSummary,
                    "Executive summary", "The engagement is complete.",
                    ["Finding: Cut-off error"])],
                "Fake", "fake-model", Guid.NewGuid());

        [Fact]
        public void A_generated_report_starts_awaiting_review() {
            var report = Draft();

            Assert.Equal(GeneratedReportStatus.Draft, report.Status);
            Assert.True(report.IsAwaitingReview);
            Assert.Null(report.ReviewedBy);
            Assert.Null(report.ReviewedAt);
        }

        [Fact]
        public void A_report_with_no_sections_is_rejected() {
            var failure = Assert.Throws<DomainRuleException>(() =>
                GeneratedAuditReport.Draft(Guid.NewGuid(), "audit.report_draft",
                    AiReportScopes.FullDraft, "Empty", [], "Fake", "fake-model",
                    Guid.NewGuid()));

            Assert.Contains("at least one section", failure.Message);
        }

        [Fact]
        public void Accepting_without_review_authority_is_refused() {
            var report = Draft();

            Assert.Throws<DomainRuleException>(() =>
                report.Accept(Guid.NewGuid(), hasReviewAuthority: false, null));

            Assert.True(report.IsAwaitingReview);
        }

        [Fact]
        public void Accepting_with_authority_records_who_took_responsibility() {
            var report = Draft();
            var reviewer = Guid.NewGuid();

            report.Accept(reviewer, hasReviewAuthority: true, "  Checked against the file.  ");

            Assert.Equal(GeneratedReportStatus.Accepted, report.Status);
            Assert.False(report.IsAwaitingReview);
            Assert.Equal(reviewer, report.ReviewedBy);
            Assert.NotNull(report.ReviewedAt);
            Assert.Equal("Checked against the file.", report.ReviewNote);
        }

        [Fact]
        public void Rejecting_requires_a_reason_for_the_record() {
            var report = Draft();

            Assert.Throws<DomainRuleException>(() =>
                report.Reject(Guid.NewGuid(), hasReviewAuthority: true, "   "));

            Assert.True(report.IsAwaitingReview);
        }

        [Fact]
        public void A_reviewed_report_cannot_be_reviewed_again() {
            var report = Draft();
            report.Reject(Guid.NewGuid(), hasReviewAuthority: true, "Not supported by the file.");

            var failure = Assert.Throws<DomainRuleException>(() =>
                report.Accept(Guid.NewGuid(), hasReviewAuthority: true, null));

            Assert.Contains("already been rejected", failure.Message);
        }

        [Fact]
        public void The_sections_the_model_cited_survive_a_round_trip() {
            var report = Draft();
            var restored = GeneratedAuditReport.Restore(report.Id, report.EngagementId,
                report.Capability, report.ReportScope, report.Title, report.Sections,
                report.Provider, report.Model, report.Status, report.GeneratedBy,
                report.GeneratedAt, report.ReviewedBy, report.ReviewedAt, report.ReviewNote);

            var section = Assert.Single(restored.Sections);
            Assert.Equal(["Finding: Cut-off error"], section.Sources);
        }
    }
}
