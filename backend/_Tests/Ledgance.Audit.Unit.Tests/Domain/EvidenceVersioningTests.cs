using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Exceptions;
using DomainEvidence = Ledgance.Audit.Engagement.Domain.Evidence;

namespace Ledgance.Audit.Unit.Tests.Domain {
    public class EvidenceVersioningTests {
        private static DomainEvidence Uploaded() =>
            DomainEvidence.Upload(Guid.NewGuid(), null, null, "bank-confirmation.pdf",
                "application/pdf", 1_000, "eng/ev/v1/bank-confirmation.pdf",
                "Initial confirmation request", EvidenceCategory.Evidence,
                ["Cash", " confirmation ", "bank", "cash"], Guid.NewGuid());

        [Fact]
        public void Superseding_retains_every_prior_version_with_its_note() {
            var evidence = Uploaded();
            var second = Guid.NewGuid();

            evidence.Supersede("eng/ev/v2/bank-confirmation.pdf", 1_100, "application/pdf",
                "Corrected account number formatting", second);
            evidence.Supersede("eng/ev/v3/bank-confirmation.pdf", 1_200, "application/pdf",
                "Updated with bank-confirmed balances", second);

            Assert.Equal(3, evidence.Version);
            Assert.Equal("Updated with bank-confirmed balances", evidence.Description);

            var versions = evidence.AllVersions();
            Assert.Equal([3, 2, 1], versions.Select(v => v.Version));
            Assert.Equal("Initial confirmation request", versions[2].Note);
            Assert.Equal("Corrected account number formatting", versions[1].Note);

            // Every version keeps its own storage path, so old files stay downloadable.
            Assert.Equal("eng/ev/v1/bank-confirmation.pdf", evidence.FindVersion(1)!.StoragePath);
            Assert.Equal("eng/ev/v3/bank-confirmation.pdf", evidence.FindVersion(3)!.StoragePath);
            Assert.Null(evidence.FindVersion(9));
        }

        [Fact]
        public void Tags_are_normalized_and_deduplicated() {
            Assert.Equal(["cash", "confirmation", "bank"], Uploaded().Tags);
        }

        [Fact]
        public void Empty_content_is_rejected_on_upload_and_supersede() {
            Assert.Throws<DomainRuleException>(() =>
                DomainEvidence.Upload(Guid.NewGuid(), null, null, "x.pdf", "application/pdf",
                    0, "path", "", EvidenceCategory.Supporting, [], Guid.NewGuid()));

            var evidence = Uploaded();
            Assert.Throws<DomainRuleException>(() =>
                evidence.Supersede("path", 0, "application/pdf", "note", Guid.NewGuid()));
        }
    }
}
