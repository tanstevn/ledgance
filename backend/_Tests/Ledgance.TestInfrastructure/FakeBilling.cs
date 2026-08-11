using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.TestInfrastructure {
    public sealed class FakeBillingGateway : IBillingGateway {
        public List<CheckoutRequest> CheckoutRequests { get; } = [];
        public List<(string SubscriptionId, string PriceId)> PlanChanges { get; } = [];
        public List<(string SubscriptionId, bool Cancel)> CancellationChanges { get; } = [];
        public Dictionary<string, BillingSubscriptionSnapshot> Subscriptions { get; } = [];

        public string CustomerId { get; set; } = "cus_fake";
        public string CheckoutUrl { get; set; } = "https://checkout.test/session";
        public string PortalUrl { get; set; } = "https://portal.test/session";
        public int CustomersCreated { get; private set; }

        /// <summary>Simulates the provider refusing to open a session.</summary>
        public bool FailCheckoutSession { get; set; }

        public Task<string> EnsureCustomerAsync(Guid organizationId, string organizationName,
            string email, string? existingCustomerId, CancellationToken ct) {
            if (!string.IsNullOrWhiteSpace(existingCustomerId)) {
                return Task.FromResult(existingCustomerId);
            }

            CustomersCreated++;
            return Task.FromResult(CustomerId);
        }

        public Task<string> CreateCheckoutSessionAsync(CheckoutRequest request,
            CancellationToken ct) {
            if (FailCheckoutSession) {
                throw new InvalidOperationException("The provider refused the session.");
            }

            CheckoutRequests.Add(request);
            return Task.FromResult(CheckoutUrl);
        }

        public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl,
            CancellationToken ct) =>
            Task.FromResult(PortalUrl);

        public Task<BillingSubscriptionSnapshot> ChangePlanAsync(string subscriptionId,
            string priceId, CancellationToken ct) {
            PlanChanges.Add((subscriptionId, priceId));

            var current = Subscriptions.GetValueOrDefault(subscriptionId)
                ?? new BillingSubscriptionSnapshot(subscriptionId, CustomerId, priceId,
                    SubscriptionStatus.Active, null, false);

            var updated = current with { PriceId = priceId, CancelAtPeriodEnd = false };
            Subscriptions[subscriptionId] = updated;

            return Task.FromResult(updated);
        }

        public Task<BillingSubscriptionSnapshot> SetCancellationAsync(string subscriptionId,
            bool cancelAtPeriodEnd, CancellationToken ct) {
            CancellationChanges.Add((subscriptionId, cancelAtPeriodEnd));

            var current = Subscriptions.GetValueOrDefault(subscriptionId)
                ?? new BillingSubscriptionSnapshot(subscriptionId, CustomerId, null,
                    SubscriptionStatus.Active, null, false);

            var updated = current with { CancelAtPeriodEnd = cancelAtPeriodEnd };
            Subscriptions[subscriptionId] = updated;

            return Task.FromResult(updated);
        }

        public Task<BillingSubscriptionSnapshot?> GetSubscriptionAsync(string subscriptionId,
            CancellationToken ct) =>
            Task.FromResult(Subscriptions.GetValueOrDefault(subscriptionId));
    }

    public sealed class InMemorySubscriptionStore : ISubscriptionStore {
        public List<StoredSubscription> Rows { get; } = [];

        public Task<StoredSubscription?> FindAsync(Guid organizationId, ProductModule module,
            CancellationToken ct) =>
            Task.FromResult(Rows.FirstOrDefault(row =>
                row.OrganizationId == organizationId && row.Module == module));

        public Task<StoredSubscription?> FindBySubscriptionIdAsync(string subscriptionId,
            CancellationToken ct) =>
            Task.FromResult(Rows.FirstOrDefault(row => row.SubscriptionId == subscriptionId));

        public Task<StoredSubscription?> FindByCustomerIdAsync(string customerId,
            CancellationToken ct) =>
            Task.FromResult(Rows.FirstOrDefault(row => row.CustomerId == customerId));

        public Task UpsertAsync(StoredSubscription subscription, CancellationToken ct) {
            Rows.RemoveAll(row => row.OrganizationId == subscription.OrganizationId
                && row.Module == subscription.Module);

            Rows.Add(subscription);
            return Task.CompletedTask;
        }
    }

    public sealed class InMemoryProcessedEventStore : IProcessedEventStore {
        public HashSet<string> EventIds { get; } = [];

        public Task<bool> TryRecordAsync(string eventId, string eventType, CancellationToken ct) =>
            Task.FromResult(EventIds.Add(eventId));
    }

    public sealed class StubPriceCatalog : IBillingPriceCatalog {
        private readonly Dictionary<PlanCode, string> _prices = [];

        public StubPriceCatalog With(PlanCode plan, string priceId) {
            _prices[plan] = priceId;
            return this;
        }

        public string? PriceIdFor(PlanCode plan) => _prices.GetValueOrDefault(plan);

        public PlanCode? PlanForPriceId(string priceId) =>
            _prices.Any(entry => entry.Value == priceId)
                ? _prices.First(entry => entry.Value == priceId).Key
                : null;
    }

    public sealed class StubPriceReader : IBillingPriceReader {
        private readonly Dictionary<PlanCode, BillingPrice> _prices = [];

        public StubPriceReader With(PlanCode plan, long amountMinorUnits,
            string currency = "USD", string interval = "month") {
            _prices[plan] = new BillingPrice($"price_{plan}", amountMinorUnits, currency,
                interval, 1);

            return this;
        }

        public Task<IReadOnlyDictionary<PlanCode, BillingPrice>> GetPricesAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<PlanCode, BillingPrice>>(_prices);
    }

    /// <summary>
    /// Stands in for signature verification: the test supplies the event a verified payload
    /// would produce, or asks for a rejection.
    /// </summary>
    public sealed class StubWebhookVerifier : IBillingWebhookVerifier {
        private BillingWebhookEvent? _next;
        private bool _rejects;

        public StubWebhookVerifier Returns(BillingWebhookEvent verified) {
            _next = verified;
            _rejects = false;
            return this;
        }

        public StubWebhookVerifier Rejects() {
            _rejects = true;
            return this;
        }

        public BillingWebhookEvent Verify(string payload, string signatureHeader) =>
            _rejects || _next is null
                ? throw new InvalidOperationException("The signature did not verify.")
                : _next;
    }

    public sealed class StubBillingUrls : IBillingUrls {
        public string CheckoutSuccessUrl => "https://app.test/subscribe/success";
        public string CheckoutCancelUrl => "https://app.test/pricing";
        public string PortalReturnUrl => "https://app.test/dashboard/billing";
    }

    /// <summary>
    /// Reads entitlements from whatever the billing path last wrote, so a test can prove that
    /// a provider event actually moves the organization's entitlements.
    /// </summary>
    public sealed class StoreBackedSubscriptionReader : ISubscriptionReader {
        private readonly ISubscriptionStore _store;

        public StoreBackedSubscriptionReader(ISubscriptionStore store) {
            _store = store;
        }

        public async Task<OrganizationSubscription> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) {
            var stored = await _store.FindAsync(organizationId, module, ct);

            return stored is null
                ? OrganizationSubscription.FreeFor(module)
                : new OrganizationSubscription(module, stored.Plan, stored.Status,
                    new Dictionary<string, string>());
        }
    }
}
