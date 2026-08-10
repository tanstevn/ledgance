using Ledgance.Accounting.Ledger.Application.Published;
using Ledgance.Integration.AccountingContext;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Integration.Unit.Tests {
    public sealed class InMemoryAccountingReadContract : IAccountingReadContract {
        public List<AccountingEntitySnapshot> Entities { get; } = [];
        public List<FiscalPeriodSnapshot> Periods { get; } = [];
        public TrialBalanceSnapshot? TrialBalance { get; set; }

        public Task<IReadOnlyList<AccountingEntitySnapshot>> ListEntitiesAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccountingEntitySnapshot>>(Entities);

        public Task<IReadOnlyList<FiscalPeriodSnapshot>> ListPeriodsAsync(Guid entityId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FiscalPeriodSnapshot>>(Periods
                .Where(period => period.EntityId == entityId)
                .ToList());

        public Task<TrialBalanceSnapshot?> GetTrialBalanceAsync(Guid entityId, Guid periodId,
            CancellationToken ct) =>
            Task.FromResult(TrialBalance);
    }

    public sealed class InMemoryAccountingLinkStore : IAccountingLinkStore {
        public bool Enabled { get; set; }

        public Task<bool> IsEnabledAsync(CancellationToken ct) => Task.FromResult(Enabled);

        public Task SetEnabledAsync(bool enabled, CancellationToken ct) {
            Enabled = enabled;
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingActivityRecorder : IActivityRecorder {
        public List<ActivityEntry> Entries { get; } = [];

        public Task RecordAsync(ActivityEntry entry, CancellationToken ct) {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    public class LinkedAccountingSourceAdapterTests {
        private readonly InMemoryAccountingReadContract _accounting = new();
        private readonly InMemoryAccountingLinkStore _link = new() { Enabled = true };
        private readonly FakeEntitlementService _entitlements = new();
        private readonly FakeCurrentUserAccessor _currentUser =
            new(TestIdentity.User(OrganizationRole.Member));

        private LinkedAccountingSourceAdapter Adapter() =>
            new(_accounting, _link, _entitlements, _currentUser);

        private void EntitleBothProducts() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);
            _entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);
        }

        [Fact]
        public async Task Availability_requires_the_audit_plan_to_include_sharing() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);

            var availability = await Adapter().GetAvailabilityAsync(CancellationToken.None);

            Assert.False(availability.IsAvailable);
            Assert.Contains("Audit plan", availability.UnavailableReason);
        }

        [Fact]
        public async Task Availability_requires_the_accounting_plan_to_include_sharing() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);
            _entitlements.With(ProductModule.Accounting, PlanCode.Free);

            var availability = await Adapter().GetAvailabilityAsync(CancellationToken.None);

            Assert.False(availability.IsAvailable);
            Assert.Contains("Accounting plan", availability.UnavailableReason);
        }

        [Fact]
        public async Task Availability_requires_the_organization_link_to_be_enabled() {
            EntitleBothProducts();
            _link.Enabled = false;

            var availability = await Adapter().GetAvailabilityAsync(CancellationToken.None);

            Assert.False(availability.IsAvailable);
            Assert.Contains("not enabled", availability.UnavailableReason);
        }

        [Fact]
        public async Task Reading_without_entitlement_throws_an_entitlement_failure() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);

            await Assert.ThrowsAsync<EntitlementException>(
                () => Adapter().ListEntitiesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Reading_with_the_link_disabled_is_a_domain_rule_failure() {
            EntitleBothProducts();
            _link.Enabled = false;

            await Assert.ThrowsAsync<DomainRuleException>(
                () => Adapter().ListEntitiesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Archived_entities_are_not_exposed_to_audit() {
            EntitleBothProducts();
            var activeId = Guid.NewGuid();
            _accounting.Entities.Add(new AccountingEntitySnapshot(activeId, "Active", "PHP",
                IsArchived: false));
            _accounting.Entities.Add(new AccountingEntitySnapshot(Guid.NewGuid(), "Archived",
                "PHP", IsArchived: true));
            _accounting.Periods.Add(new FiscalPeriodSnapshot(Guid.NewGuid(), activeId,
                "March 2026", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), "Open"));

            var entities = await Adapter().ListEntitiesAsync(CancellationToken.None);

            var entity = Assert.Single(entities);
            Assert.Equal("Active", entity.Name);
            Assert.Single(entity.Periods);
        }

        [Fact]
        public async Task The_trial_balance_is_translated_into_audit_vocabulary() {
            EntitleBothProducts();
            var entityId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            _accounting.Entities.Add(new AccountingEntitySnapshot(entityId, "Acme", "PHP",
                IsArchived: false));
            _accounting.TrialBalance = new TrialBalanceSnapshot(entityId, periodId,
                "March 2026", new DateOnly(2026, 3, 31), [
                    new TrialBalanceLineSnapshot("1010", "Cash", 750, 0),
                    new TrialBalanceLineSnapshot("4010", "Sales", 0, 750)
                ]);

            var trialBalance = await Adapter().GetTrialBalanceAsync(entityId, periodId,
                CancellationToken.None);

            Assert.NotNull(trialBalance);
            Assert.Equal("Acme", trialBalance!.EntityName);
            Assert.Equal("March 2026", trialBalance.PeriodName);
            Assert.Equal(2, trialBalance.Lines.Count);
            Assert.Equal(750m, trialBalance.Lines
                .Single(line => line.AccountCode == "1010").Debit);
        }
    }

    public class AccountingLinkSliceTests {
        private readonly InMemoryAccountingLinkStore _link = new();
        private readonly RecordingActivityRecorder _activity = new();

        private MediatorTestHarness Harness(CurrentUser user) =>
            new MediatorTestHarness(user)
                .WithHandler<SetAccountingLinkCommand, Result<bool>,
                    SetAccountingLinkCommandHandler>()
                .WithHandler<GetAccountingLinkStatusQuery, Result<AccountingLinkStatusView>,
                    GetAccountingLinkStatusQueryHandler>()
                .WithService<IAccountingLinkStore>(_link)
                .WithService<IActivityRecorder>(_activity);

        private static CurrentUser Admin() =>
            TestIdentity.User(OrganizationRole.Admin,
                permissions: [AccountingLinkPermissions.Read,
                    AccountingLinkPermissions.Manage]);

        [Fact]
        public async Task A_member_cannot_change_the_link() {
            var harness = Harness(TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLinkPermissions.Read]));

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(new SetAccountingLinkCommand { Enabled = true }));

            Assert.False(_link.Enabled);
        }

        [Fact]
        public async Task Enabling_requires_the_entitlement_on_both_products() {
            var harness = Harness(Admin());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free);

            await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(new SetAccountingLinkCommand { Enabled = true }));

            Assert.False(_link.Enabled);
        }

        [Fact]
        public async Task An_admin_enables_the_link_and_activity_is_recorded() {
            var harness = Harness(Admin());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);

            var result = await harness.SendAsync(
                new SetAccountingLinkCommand { Enabled = true });

            Assert.True(result.Successful);
            Assert.True(_link.Enabled);

            var entry = Assert.Single(_activity.Entries);
            Assert.Equal("accounting_link.enabled", entry.Action);
            Assert.Equal("Integration", entry.Module);
        }

        [Fact]
        public async Task Disabling_never_requires_an_entitlement() {
            _link.Enabled = true;
            var harness = Harness(Admin());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.Free);
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free);

            var result = await harness.SendAsync(
                new SetAccountingLinkCommand { Enabled = false });

            Assert.True(result.Successful);
            Assert.False(_link.Enabled);
        }

        [Fact]
        public async Task The_status_view_combines_link_and_entitlements() {
            _link.Enabled = true;
            var harness = Harness(Admin());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free);

            var status = (await harness.SendAsync(new GetAccountingLinkStatusQuery())).Data!;

            Assert.True(status.LinkEnabled);
            Assert.True(status.AuditPlanIncludesSharing);
            Assert.False(status.AccountingPlanIncludesSharing);
            Assert.False(status.IsActive);

            harness.Entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);
            var active = (await harness.SendAsync(new GetAccountingLinkStatusQuery())).Data!;

            Assert.True(active.IsActive);
        }
    }
}
