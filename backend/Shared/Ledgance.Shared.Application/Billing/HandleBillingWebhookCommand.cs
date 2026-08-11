using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Ledgance.Shared.Application.Billing {
    /// <summary>
    /// Provider webhooks are the source of truth for subscription state (ADR-007). The request
    /// is anonymous because the caller is the payment provider, not a user; its authenticity
    /// comes from the payload signature, which is verified before anything is interpreted.
    /// </summary>
    [AllowAnonymousRequest]
    public class HandleBillingWebhookCommand : ICommand<Result<string>> {
        public string Payload { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    public class HandleBillingWebhookCommandHandler
        : IRequestHandler<HandleBillingWebhookCommand, Result<string>> {
        private readonly IBillingWebhookVerifier _verifier;
        private readonly IBillingGateway _billing;
        private readonly IBillingPriceCatalog _prices;
        private readonly ISubscriptionStore _subscriptions;
        private readonly IProcessedEventStore _processedEvents;
        private readonly ILogger<HandleBillingWebhookCommandHandler> _logger;

        public HandleBillingWebhookCommandHandler(IBillingWebhookVerifier verifier,
            IBillingGateway billing, IBillingPriceCatalog prices,
            ISubscriptionStore subscriptions, IProcessedEventStore processedEvents,
            ILogger<HandleBillingWebhookCommandHandler> logger) {
            _verifier = verifier;
            _billing = billing;
            _prices = prices;
            _subscriptions = subscriptions;
            _processedEvents = processedEvents;
            _logger = logger;
        }

        public async Task<Result<string>> HandleAsync(HandleBillingWebhookCommand request,
            CancellationToken ct) {
            BillingWebhookEvent verified;

            try {
                verified = _verifier.Verify(request.Payload, request.Signature);
            }
            catch (Exception exception) {
                _logger.LogWarning("Rejected a billing webhook: {Reason}", exception.Message);
                return Result<string>.Error("The webhook signature could not be verified.");
            }

            if (verified.Kind is BillingEventKind.Ignored) {
                return Result<string>.Success("ignored");
            }

            if (!await _processedEvents.TryRecordAsync(verified.EventId, verified.EventType, ct)) {
                return Result<string>.Success("duplicate");
            }

            var stored = await ResolveAsync(verified, ct);

            if (stored is null) {
                _logger.LogWarning(
                    "Billing event {EventType} could not be matched to an organization.",
                    verified.EventType);

                return Result<string>.Success("unmatched");
            }

            // Providers deliver out of order; an older event must never undo newer state.
            if (stored.LastEventAt is { } last && last > verified.OccurredAt) {
                return Result<string>.Success("stale");
            }

            // Events that only reference a subscription (checkout completion, invoices) are
            // resolved by asking the provider for the current state rather than inferring it.
            var subscriptionId = verified.SubscriptionIdHint ?? stored.SubscriptionId;

            var snapshot = verified.Subscription
                ?? (subscriptionId is null
                    ? null
                    : await _billing.GetSubscriptionAsync(subscriptionId, ct));

            if (snapshot is null) {
                return Result<string>.Success("unmatched");
            }

            var updated = verified.Kind is BillingEventKind.SubscriptionEnded
                ? stored with {
                    Status = SubscriptionStatus.Canceled,
                    CancelAtPeriodEnd = false,
                    CurrentPeriodEnd = snapshot.CurrentPeriodEnd,
                    LastEventAt = verified.OccurredAt
                }
                : stored with {
                    Plan = PlanFor(snapshot, verified, stored),
                    Status = snapshot.Status,
                    CustomerId = snapshot.CustomerId,
                    SubscriptionId = snapshot.SubscriptionId,
                    CurrentPeriodEnd = snapshot.CurrentPeriodEnd,
                    CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd,
                    LastEventAt = verified.OccurredAt
                };

            await _subscriptions.UpsertAsync(updated, ct);

            _logger.LogInformation(
                "Billing event {EventType} applied: {Module} is now {Plan}/{Status}.",
                verified.EventType, updated.Module, updated.Plan, updated.Status);

            return Result<string>.Success("applied");
        }

        /// <summary>
        /// The price the provider bills is the authority on which plan is active, so a change
        /// made in the provider's own portal lands here too; checkout metadata covers the first
        /// event, before any price is known to us.
        /// </summary>
        private PlanCode PlanFor(BillingSubscriptionSnapshot snapshot,
            BillingWebhookEvent verified, StoredSubscription stored) =>
            (snapshot.PriceId is null ? null : _prices.PlanForPriceId(snapshot.PriceId))
                ?? verified.Plan
                ?? stored.Plan;

        private async Task<StoredSubscription?> ResolveAsync(BillingWebhookEvent verified,
            CancellationToken ct) {
            if (verified.OrganizationId is { } organizationId && verified.Module is { } module) {
                return await _subscriptions.FindAsync(organizationId, module, ct)
                    ?? new StoredSubscription(organizationId, module, PlanCode.Free,
                        SubscriptionStatus.Canceled, verified.Subscription?.CustomerId,
                        verified.Subscription?.SubscriptionId, null, false, null);
            }

            var subscriptionId = verified.Subscription?.SubscriptionId
                ?? verified.SubscriptionIdHint;
            var customerId = verified.Subscription?.CustomerId ?? verified.CustomerIdHint;

            if (subscriptionId is not null) {
                var bySubscription =
                    await _subscriptions.FindBySubscriptionIdAsync(subscriptionId, ct);

                if (bySubscription is not null) {
                    return bySubscription;
                }
            }

            return customerId is null
                ? null
                : await _subscriptions.FindByCustomerIdAsync(customerId, ct);
        }
    }
}
