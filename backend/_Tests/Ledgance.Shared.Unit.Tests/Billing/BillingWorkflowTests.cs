using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Subscriptions;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace Ledgance.Shared.Unit.Tests.Billing {
    /// <summary>
    /// The billing slices through the real pipeline, over a fake provider. These cover the
    /// paths money and access depend on: who may buy, what may be bought, and how provider
    /// events move an organization's entitlements.
    /// </summary>
    public class BillingWorkflowTests {
        private const string SoloPrice = "price_solo";
        private const string TeamPrice = "price_team";
        private const string SubscriptionId = "sub_test";

        private readonly FakeBillingGateway _gateway = new();
        private readonly InMemorySubscriptionStore _store = new();
        private readonly InMemoryProcessedEventStore _events = new();
        private readonly StubPriceCatalog _prices = new StubPriceCatalog()
            .With(PlanCode.AccountingSolo, SoloPrice)
            .With(PlanCode.AccountingTeam, TeamPrice);
        private readonly StubWebhookVerifier _verifier = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly StubOrganizationDirectory _organizations = new();

        private MediatorTestHarness Harness(CurrentUser? user) =>
            new MediatorTestHarness(user)
                .WithHandler<StartCheckoutCommand, Result<StartCheckoutResult>,
                    StartCheckoutCommandHandler>()
                .WithHandler<CreateBillingPortalSessionCommand, Result<BillingPortalResult>,
                    CreateBillingPortalSessionCommandHandler>()
                .WithHandler<ChangeSubscriptionPlanCommand, Result<bool>,
                    ChangeSubscriptionPlanCommandHandler>()
                .WithHandler<SetSubscriptionCancellationCommand, Result<bool>,
                    SetSubscriptionCancellationCommandHandler>()
                .WithHandler<GetBillingOverviewQuery, Result<BillingOverview>,
                    GetBillingOverviewQueryHandler>()
                .WithHandler<HandleBillingWebhookCommand, Result<string>,
                    HandleBillingWebhookCommandHandler>()
                .WithValidator(new StartCheckoutCommandValidator())
                .WithValidator(new ChangeSubscriptionPlanCommandValidator())
                .WithService<IBillingGateway>(_gateway)
                .WithService<IBillingPriceCatalog>(_prices)
                .WithService<IBillingWebhookVerifier>(_verifier)
                .WithService<ISubscriptionStore>(_store)
                .WithService<IProcessedEventStore>(_events)
                .WithService<IBillingUrls>(new StubBillingUrls())
                .WithService<IOrganizationDirectory>(_organizations)
                .WithService<IActivityRecorder>(_activity);

        private static CurrentUser Owner() =>
            TestIdentity.User(OrganizationRole.Owner,
                permissions: [SharedPermissions.BillingManage, SharedPermissions.BillingRead]);

        private static CurrentUser BillingReader() =>
            TestIdentity.User(OrganizationRole.Admin,
                permissions: SharedPermissions.BillingRead);

        /// <summary>Entitlements resolved the way production does, from what billing stored.</summary>
        private EntitlementService ResolveEntitlements() =>
            new(new StoreBackedSubscriptionReader(_store),
                Options.Create(new SubscriptionSettings()));

        private BillingWebhookEvent SubscriptionEvent(Guid organizationId, string eventId,
            SubscriptionStatus status, DateTime occurredAt, string priceId = SoloPrice,
            BillingEventKind kind = BillingEventKind.SubscriptionChanged,
            bool cancelAtPeriodEnd = false) =>
            new(eventId, "customer.subscription.updated", kind, occurredAt,
                new BillingSubscriptionSnapshot(SubscriptionId, "cus_fake", priceId, status,
                    occurredAt.AddDays(30), cancelAtPeriodEnd),
                organizationId, ProductModule.Accounting, PlanCode.AccountingSolo);

        private static HandleBillingWebhookCommand Webhook() =>
            new() { Payload = "{}", Signature = "signature" };

        [Fact]
        public async Task Checkout_requires_the_billing_manage_permission() {
            var harness = Harness(BillingReader());

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                harness.SendAsync(new StartCheckoutCommand {
                    PlanCode = nameof(PlanCode.AccountingSolo)
                }));

            Assert.Empty(_gateway.CheckoutRequests);
        }

        [Fact]
        public async Task Neither_the_free_plan_nor_an_enterprise_plan_reaches_checkout() {
            var harness = Harness(Owner());

            var free = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.Free)
            });

            var enterprise = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AccountingEnterprise)
            });

            Assert.False(free.Successful);
            Assert.False(enterprise.Successful);
            Assert.Contains("sales", string.Join(" ", enterprise.Errors ?? []),
                StringComparison.OrdinalIgnoreCase);
            Assert.Empty(_gateway.CheckoutRequests);
        }

        [Fact]
        public async Task A_plan_without_a_configured_price_cannot_be_bought() {
            var harness = Harness(Owner());

            var result = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AuditProfessional)
            });

            Assert.False(result.Successful);
            Assert.Empty(_gateway.CheckoutRequests);
        }

        [Fact]
        public async Task Checkout_returns_the_session_url_and_carries_the_scope_as_metadata() {
            var user = Owner();
            var harness = Harness(user);

            var result = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AccountingSolo)
            });

            Assert.True(result.Successful);
            Assert.Equal(_gateway.CheckoutUrl, result.Data!.CheckoutUrl);

            var request = Assert.Single(_gateway.CheckoutRequests);
            Assert.Equal(user.OrganizationId, request.OrganizationId);
            Assert.Equal(ProductModule.Accounting, request.Module);
            Assert.Equal(PlanCode.AccountingSolo, request.Plan);
            Assert.Equal(SoloPrice, request.PriceId);

            // The customer is stored before the session completes, so a retry reuses it.
            var stored = Assert.Single(_store.Rows);
            Assert.Equal(_gateway.CustomerId, stored.CustomerId);

            await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AccountingSolo)
            });

            Assert.Equal(1, _gateway.CustomersCreated);
        }

        [Fact]
        public async Task A_failure_after_the_customer_exists_does_not_orphan_it() {
            var user = Owner();
            var harness = Harness(user);

            _gateway.FailCheckoutSession = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.SendAsync(new StartCheckoutCommand {
                    PlanCode = nameof(PlanCode.AccountingSolo)
                }));

            var stored = Assert.Single(_store.Rows);
            Assert.Equal(_gateway.CustomerId, stored.CustomerId);

            _gateway.FailCheckoutSession = false;

            var retry = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AccountingSolo)
            });

            Assert.True(retry.Successful);
            Assert.Equal(1, _gateway.CustomersCreated);
        }

        [Fact]
        public async Task A_product_with_an_active_subscription_changes_plan_instead_of_buying_again() {
            var user = Owner();
            var harness = Harness(user);

            await _store.UpsertAsync(new StoredSubscription(user.OrganizationId,
                ProductModule.Accounting, PlanCode.AccountingSolo, SubscriptionStatus.Active,
                "cus_fake", SubscriptionId, DateTime.UtcNow.AddDays(20), false,
                DateTime.UtcNow), default);

            var result = await harness.SendAsync(new StartCheckoutCommand {
                PlanCode = nameof(PlanCode.AccountingTeam)
            });

            Assert.False(result.Successful);
            Assert.Empty(_gateway.CheckoutRequests);
        }

        [Fact]
        public async Task An_unverifiable_webhook_is_rejected_and_changes_nothing() {
            var harness = Harness(null);
            _verifier.Rejects();

            var result = await harness.SendAsync(Webhook());

            Assert.False(result.Successful);
            Assert.Empty(_store.Rows);
            Assert.Empty(_events.EventIds);
        }

        [Fact]
        public async Task A_subscription_event_activates_the_plan_and_entitlements_follow() {
            var user = Owner();
            var harness = Harness(user);
            var entitlements = ResolveEntitlements();

            var before = await entitlements.GetAsync(user.OrganizationId,
                ProductModule.Accounting, default);
            Assert.Equal(PlanCode.Free, before.Plan);
            Assert.Equal(1L, before.Limit(Entitlements.MaxEntities));

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_1",
                SubscriptionStatus.Active, DateTime.UtcNow));

            var result = await harness.SendAsync(Webhook());
            Assert.True(result.Successful);

            var after = await ResolveEntitlements().GetAsync(user.OrganizationId,
                ProductModule.Accounting, default);

            Assert.Equal(PlanCode.AccountingSolo, after.Plan);
            Assert.Equal(3L, after.Limit(Entitlements.MaxEntities));
        }

        [Fact]
        public async Task A_repeated_delivery_of_the_same_event_is_ignored() {
            var user = Owner();
            var harness = Harness(user);

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_1",
                SubscriptionStatus.Active, DateTime.UtcNow));

            var first = await harness.SendAsync(Webhook());
            var second = await harness.SendAsync(Webhook());

            Assert.Equal("applied", first.Data);
            Assert.Equal("duplicate", second.Data);
            Assert.Single(_store.Rows);
        }

        [Fact]
        public async Task An_out_of_order_event_does_not_undo_newer_state() {
            var user = Owner();
            var harness = Harness(user);
            var now = DateTime.UtcNow;

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_new",
                SubscriptionStatus.Active, now, TeamPrice));
            await harness.SendAsync(Webhook());

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_old",
                SubscriptionStatus.Canceled, now.AddMinutes(-10)));

            var result = await harness.SendAsync(Webhook());

            Assert.Equal("stale", result.Data);

            var stored = Assert.Single(_store.Rows);
            Assert.Equal(PlanCode.AccountingTeam, stored.Plan);
            Assert.Equal(SubscriptionStatus.Active, stored.Status);
        }

        [Fact]
        public async Task A_deleted_subscription_falls_back_to_the_free_plan() {
            var user = Owner();
            var harness = Harness(user);
            var now = DateTime.UtcNow;

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_1",
                SubscriptionStatus.Active, now));
            await harness.SendAsync(Webhook());

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_2",
                SubscriptionStatus.Canceled, now.AddMinutes(5),
                kind: BillingEventKind.SubscriptionEnded));
            await harness.SendAsync(Webhook());

            var entitlements = await ResolveEntitlements().GetAsync(user.OrganizationId,
                ProductModule.Accounting, default);

            Assert.Equal(PlanCode.Free, entitlements.Plan);
            Assert.Equal(1L, entitlements.Limit(Entitlements.MaxEntities));
        }

        [Fact]
        public async Task A_plan_change_in_the_provider_portal_is_adopted_from_the_price() {
            var user = Owner();
            var harness = Harness(user);
            var now = DateTime.UtcNow;

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_1",
                SubscriptionStatus.Active, now));
            await harness.SendAsync(Webhook());

            _verifier.Returns(SubscriptionEvent(user.OrganizationId, "evt_2",
                SubscriptionStatus.Active, now.AddMinutes(1), TeamPrice));
            await harness.SendAsync(Webhook());

            var stored = Assert.Single(_store.Rows);
            Assert.Equal(PlanCode.AccountingTeam, stored.Plan);
        }

        [Fact]
        public async Task Cancelling_keeps_the_plan_until_the_period_ends() {
            var user = Owner();
            var harness = Harness(user);
            var periodEnd = DateTime.UtcNow.AddDays(12);

            await _store.UpsertAsync(new StoredSubscription(user.OrganizationId,
                ProductModule.Accounting, PlanCode.AccountingSolo, SubscriptionStatus.Active,
                "cus_fake", SubscriptionId, periodEnd, false, DateTime.UtcNow), default);

            _gateway.Subscriptions[SubscriptionId] = new BillingSubscriptionSnapshot(
                SubscriptionId, "cus_fake", SoloPrice, SubscriptionStatus.Active, periodEnd,
                false);

            var result = await harness.SendAsync(new SetSubscriptionCancellationCommand {
                Module = nameof(ProductModule.Accounting)
            });

            Assert.True(result.Successful);
            Assert.Equal((SubscriptionId, true), Assert.Single(_gateway.CancellationChanges));

            var stored = Assert.Single(_store.Rows);
            Assert.True(stored.CancelAtPeriodEnd);
            Assert.Equal(SubscriptionStatus.Active, stored.Status);

            // Access continues until the provider actually ends the subscription.
            var entitlements = await ResolveEntitlements().GetAsync(user.OrganizationId,
                ProductModule.Accounting, default);
            Assert.Equal(PlanCode.AccountingSolo, entitlements.Plan);
        }

        [Fact]
        public async Task Changing_plan_moves_the_subscription_to_the_new_price() {
            var user = Owner();
            var harness = Harness(user);

            await _store.UpsertAsync(new StoredSubscription(user.OrganizationId,
                ProductModule.Accounting, PlanCode.AccountingSolo, SubscriptionStatus.Active,
                "cus_fake", SubscriptionId, DateTime.UtcNow.AddDays(10), false,
                DateTime.UtcNow), default);

            var result = await harness.SendAsync(new ChangeSubscriptionPlanCommand {
                PlanCode = nameof(PlanCode.AccountingTeam)
            });

            Assert.True(result.Successful);
            Assert.Equal((SubscriptionId, TeamPrice), Assert.Single(_gateway.PlanChanges));

            var stored = Assert.Single(_store.Rows);
            Assert.Equal(PlanCode.AccountingTeam, stored.Plan);
        }

        [Fact]
        public async Task Changing_plan_without_a_subscription_asks_for_checkout_instead() {
            var harness = Harness(Owner());

            var result = await harness.SendAsync(new ChangeSubscriptionPlanCommand {
                PlanCode = nameof(PlanCode.AccountingTeam)
            });

            Assert.False(result.Successful);
            Assert.Empty(_gateway.PlanChanges);
        }

        [Fact]
        public async Task The_overview_reports_both_products_and_needs_the_read_permission() {
            var user = Owner();
            var harness = Harness(user);

            await _store.UpsertAsync(new StoredSubscription(user.OrganizationId,
                ProductModule.Accounting, PlanCode.AccountingSolo, SubscriptionStatus.Active,
                "cus_fake", SubscriptionId, DateTime.UtcNow.AddDays(9), true,
                DateTime.UtcNow), default);

            harness.Entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);
            harness.Entitlements.With(ProductModule.Audit, PlanCode.Free);

            var result = await harness.SendAsync(new GetBillingOverviewQuery());

            Assert.True(result.Successful);
            Assert.Equal(2, result.Data!.Products.Count);

            var accounting = result.Data.Products
                .Single(product => product.Module == nameof(ProductModule.Accounting));

            Assert.Equal(nameof(PlanCode.AccountingSolo), accounting.Plan);
            Assert.True(accounting.CancelAtPeriodEnd);
            Assert.True(accounting.HasSubscription);
            Assert.False(accounting.RequiresContactSales);

            var audit = result.Data.Products
                .Single(product => product.Module == nameof(ProductModule.Audit));

            Assert.Equal(nameof(PlanCode.Free), audit.Plan);
            Assert.False(audit.HasSubscription);

            var member = Harness(TestIdentity.User(OrganizationRole.Member));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                member.SendAsync(new GetBillingOverviewQuery()));
        }

        [Fact]
        public async Task The_billing_portal_needs_a_customer_to_manage() {
            var user = Owner();
            var harness = Harness(user);

            var missing = await harness.SendAsync(new CreateBillingPortalSessionCommand {
                Module = nameof(ProductModule.Accounting)
            });

            Assert.False(missing.Successful);

            await _store.UpsertAsync(new StoredSubscription(user.OrganizationId,
                ProductModule.Accounting, PlanCode.AccountingSolo, SubscriptionStatus.Active,
                "cus_fake", SubscriptionId, null, false, null), default);

            var opened = await harness.SendAsync(new CreateBillingPortalSessionCommand {
                Module = nameof(ProductModule.Accounting)
            });

            Assert.True(opened.Successful);
            Assert.Equal(_gateway.PortalUrl, opened.Data!.PortalUrl);
        }
    }

    internal sealed class RecordingActivityRecorder : IActivityRecorder {
        public List<ActivityEntry> Entries { get; } = [];

        public Task RecordAsync(ActivityEntry entry, CancellationToken ct) {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    internal sealed class StubOrganizationDirectory : IOrganizationDirectory {
        public Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail, string? product,
            CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());

        public Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(Guid organizationId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrganizationMemberInfo>>([]);

        public Task<OrganizationInfo?> GetOrganizationAsync(Guid organizationId,
            CancellationToken ct) =>
            Task.FromResult<OrganizationInfo?>(new OrganizationInfo("Test organization",
                ["Audit", "Accounting"]));

        public Task AddProductAsync(Guid organizationId, string product, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId, Guid userId,
            CancellationToken ct) =>
            Task.FromResult<OrganizationMemberInfo?>(null);
    }
}
