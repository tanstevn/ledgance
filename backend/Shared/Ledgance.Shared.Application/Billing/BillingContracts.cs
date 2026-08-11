using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Application.Billing {
    /// <summary>
    /// The subscription state the application stores for one organization and product, mirrored
    /// from the payment provider. <see cref="LastEventAt"/> is the provider's timestamp for the
    /// change, so an out-of-order webhook cannot overwrite newer state (ADR-007).
    /// </summary>
    public sealed record StoredSubscription(
        Guid OrganizationId,
        ProductModule Module,
        PlanCode Plan,
        SubscriptionStatus Status,
        string? CustomerId,
        string? SubscriptionId,
        DateTime? CurrentPeriodEnd,
        bool CancelAtPeriodEnd,
        DateTime? LastEventAt);

    /// <summary>Provider-side subscription facts, normalized away from any provider vocabulary.</summary>
    public sealed record BillingSubscriptionSnapshot(
        string SubscriptionId,
        string CustomerId,
        string? PriceId,
        SubscriptionStatus Status,
        DateTime? CurrentPeriodEnd,
        bool CancelAtPeriodEnd);

    public sealed record CheckoutRequest(
        Guid OrganizationId,
        ProductModule Module,
        PlanCode Plan,
        string PriceId,
        string CustomerId,
        string SuccessUrl,
        string CancelUrl);

    /// <summary>
    /// Everything the application needs from a payment provider. The provider is named nowhere
    /// outside Infrastructure, so the billing slices stay testable and replaceable.
    /// </summary>
    public interface IBillingGateway {
        Task<string> EnsureCustomerAsync(Guid organizationId, string organizationName,
            string email, string? existingCustomerId, CancellationToken ct);

        Task<string> CreateCheckoutSessionAsync(CheckoutRequest request, CancellationToken ct);

        Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl,
            CancellationToken ct);

        Task<BillingSubscriptionSnapshot> ChangePlanAsync(string subscriptionId, string priceId,
            CancellationToken ct);

        Task<BillingSubscriptionSnapshot> SetCancellationAsync(string subscriptionId,
            bool cancelAtPeriodEnd, CancellationToken ct);

        Task<BillingSubscriptionSnapshot?> GetSubscriptionAsync(string subscriptionId,
            CancellationToken ct);
    }

    /// <summary>
    /// Plan-to-price mapping. Price identifiers are configuration, never source, and a plan with
    /// no configured price simply cannot be bought — it never falls back to another plan's price.
    /// </summary>
    public interface IBillingPriceCatalog {
        string? PriceIdFor(PlanCode plan);
        PlanCode? PlanForPriceId(string priceId);
    }

    /// <summary>
    /// What a plan actually costs, as the provider bills it. <paramref name="AmountMinorUnits"/>
    /// is in the currency's smallest unit (1499 = 14.99) because that is how providers quote
    /// money; the presentation layer divides.
    /// </summary>
    public sealed record BillingPrice(
        string PriceId,
        long AmountMinorUnits,
        string Currency,
        string Interval,
        int IntervalCount);

    /// <summary>
    /// Reads live prices for the configured plans. The published price is the one the provider
    /// will charge, so pricing surfaces cannot drift from the invoice. Implementations cache and
    /// must degrade to "no price" rather than throwing — the pricing page is anonymous and must
    /// render even when the provider is unreachable.
    /// </summary>
    public interface IBillingPriceReader {
        Task<IReadOnlyDictionary<PlanCode, BillingPrice>> GetPricesAsync(CancellationToken ct);
    }

    public interface ISubscriptionStore {
        Task<StoredSubscription?> FindAsync(Guid organizationId, ProductModule module,
            CancellationToken ct);

        Task<StoredSubscription?> FindBySubscriptionIdAsync(string subscriptionId,
            CancellationToken ct);

        Task<StoredSubscription?> FindByCustomerIdAsync(string customerId,
            CancellationToken ct);

        Task UpsertAsync(StoredSubscription subscription, CancellationToken ct);
    }

    /// <summary>
    /// Records which provider events have already been applied. Providers retry deliveries, so
    /// handling must be idempotent.
    /// </summary>
    public interface IProcessedEventStore {
        Task<bool> TryRecordAsync(string eventId, string eventType, CancellationToken ct);
    }

    public enum BillingEventKind {
        SubscriptionChanged,
        SubscriptionEnded,
        PaymentStateChanged,
        Ignored
    }

    /// <summary>
    /// A signature-verified provider event, reduced to what the application acts on. The
    /// organization, module and plan travel as metadata set when checkout was created, so a
    /// forged payload cannot point an event at another organization without a valid signature.
    /// </summary>
    public sealed record BillingWebhookEvent(
        string EventId,
        string EventType,
        BillingEventKind Kind,
        DateTime OccurredAt,
        BillingSubscriptionSnapshot? Subscription,
        Guid? OrganizationId,
        ProductModule? Module,
        PlanCode? Plan,
        string? SubscriptionIdHint = null,
        string? CustomerIdHint = null);

    public interface IBillingWebhookVerifier {
        /// <summary>
        /// Verifies the payload signature and normalizes the event. Throws when the signature
        /// does not verify — an unverified payload is never interpreted.
        /// </summary>
        BillingWebhookEvent Verify(string payload, string signatureHeader);
    }
}
