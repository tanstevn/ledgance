using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Entities;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class EntityWorkflowTests {
        private static LedgerHarness Manager() =>
            new(TestIdentity.User(OrganizationRole.Manager,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute,
                    AccountingLedgerPermissions.Manage]));

        private static CreateEntityCommand ValidCommand(string name = "Acme Trading") =>
            new() { Name = name, LegalName = "Acme Trading Corp.", BaseCurrency = "PHP" };

        [Fact]
        public async Task A_member_without_the_manage_permission_cannot_create_an_entity() {
            var harness = new LedgerHarness(TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute]));

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(ValidCommand()));
        }

        [Fact]
        public async Task A_manager_creates_an_entity_and_activity_is_recorded() {
            var harness = Manager();
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);

            var result = await harness.SendAsync(ValidCommand());

            Assert.True(result.Successful);
            Assert.Single(harness.Entities.Entities);

            var entry = Assert.Single(harness.Activity.Entries);
            Assert.Equal("entity.created", entry.Action);
            Assert.Equal("Accounting", entry.Module);
            Assert.Equal(result.Data, entry.ContextId);

            Assert.Equal("created the entity Acme Trading.", entry.Summary);
            Assert.True(char.IsLower(entry.Summary[0]));
        }

        [Fact]
        public async Task The_free_plan_entity_limit_is_enforced() {
            var harness = Manager();
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free);

            var first = await harness.SendAsync(ValidCommand("First Books"));
            Assert.True(first.Successful);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(ValidCommand("Second Books")));

            Assert.Contains(Entitlements.MaxEntities, exception.Message);
            Assert.Single(harness.Entities.Entities);
        }

        [Fact]
        public async Task An_entity_with_an_open_period_cannot_be_archived() {
            var harness = Manager();
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            harness.Entities.Entities.Add(entity);
            harness.Periods.Periods.Add(FiscalPeriod.Open(entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

            await Assert.ThrowsAsync<DomainRuleException>(
                () => harness.SendAsync(new ArchiveEntityCommand { EntityId = entity.Id }));
        }

        [Fact]
        public async Task The_paged_entity_list_returns_one_page_with_its_period_counts() {
            var harness = Manager();

            foreach (var index in Enumerable.Range(1, 12)) {
                harness.Entities.Entities.Add(
                    AccountingEntity.Create($"Books {index:00}", "", "PHP"));
            }

            var first = harness.Entities.Entities[0];
            harness.Periods.Periods.Add(FiscalPeriod.Open(first.Id, "Jan 2026",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
            harness.Periods.Periods.Add(FiscalPeriod.Open(first.Id, "Feb 2026",
                new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));

            var page = await harness.SendAsync(
                new GetPaginatedEntitiesQuery { Page = 1, PageSize = 10 });

            Assert.True(page.Successful);
            Assert.Equal(10, page.Data!.Count());
            Assert.Equal(12, page.TotalResultsCount);
            Assert.Equal(2, page.TotalPages);

            var counted = page.Data!.Single(row => row.Id == first.Id);
            Assert.Equal(2, counted.OpenPeriods);
            Assert.Equal(2, counted.TotalPeriods);

            var second = await harness.SendAsync(
                new GetPaginatedEntitiesQuery { Page = 2, PageSize = 10 });

            Assert.Equal(2, second.Data!.Count());
        }

        [Fact]
        public async Task A_viewer_can_list_entities_but_not_modify_them() {
            var harness = new LedgerHarness(TestIdentity.User(OrganizationRole.Viewer,
                permissions: [AccountingLedgerPermissions.Read]));
            harness.Entities.Entities.Add(AccountingEntity.Create("Acme", "", "PHP"));

            var list = await harness.SendAsync(new GetEntitiesQuery());
            Assert.True(list.Successful);
            Assert.Single(list.Data!);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(ValidCommand()));
        }
    }
}
