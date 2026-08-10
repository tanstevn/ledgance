using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Documents;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class DocumentWorkflowTests {
        private readonly LedgerHarness _harness;
        private readonly AccountingEntity _entity;

        public DocumentWorkflowTests() {
            _harness = new LedgerHarness(TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute]));

            _entity = AccountingEntity.Create("Acme", "", "PHP");
            _harness.Entities.Entities.Add(_entity);
        }

        private UploadDocumentCommand Upload(string fileName = "invoice.pdf",
            int sizeBytes = 1024) =>
            new() {
                EntityId = _entity.Id,
                FileName = fileName,
                ContentType = "application/pdf",
                Description = "March supplier invoice",
                Content = new byte[sizeBytes]
            };

        [Fact]
        public async Task Uploading_stores_the_file_and_records_activity() {
            var result = await _harness.SendAsync(Upload());

            Assert.True(result.Successful);
            Assert.Single(_harness.Documents.Documents);
            Assert.Single(_harness.FileStore.UploadedPaths);
            Assert.Contains(_harness.Activity.Entries,
                entry => entry.Action == "document.uploaded");

            var url = await _harness.SendAsync(new GetDocumentDownloadUrlQuery {
                EntityId = _entity.Id,
                DocumentId = result.Data
            });

            Assert.True(url.Successful);
            Assert.Contains("signed=true", url.Data);
        }

        [Fact]
        public async Task The_storage_limit_is_enforced() {
            _harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free,
                new Dictionary<string, string> {
                    [Entitlements.StorageBytes] = "1500"
                });

            var first = await _harness.SendAsync(Upload("first.pdf", 1024));
            Assert.True(first.Successful);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => _harness.SendAsync(Upload("second.pdf", 1024)));

            Assert.Contains(Entitlements.StorageBytes, exception.Message);
            Assert.Single(_harness.Documents.Documents);
        }

        [Fact]
        public async Task Uploading_to_an_archived_entity_is_rejected() {
            _entity.Archive(hasOpenPeriods: false);

            await Assert.ThrowsAsync<DomainRuleException>(
                () => _harness.SendAsync(Upload()));
        }

        [Fact]
        public async Task Documents_filter_by_their_journal_entry_link() {
            var entryId = Guid.NewGuid();
            var linked = Upload("receipt.png");
            linked.JournalEntryId = entryId;

            await _harness.SendAsync(linked);
            await _harness.SendAsync(Upload("unlinked.pdf"));

            var filtered = await _harness.SendAsync(new GetDocumentsQuery {
                EntityId = _entity.Id,
                JournalEntryId = entryId
            });

            Assert.Single(filtered.Data!);
            Assert.Equal("receipt.png", filtered.Data!.Single().FileName);
        }
    }
}
