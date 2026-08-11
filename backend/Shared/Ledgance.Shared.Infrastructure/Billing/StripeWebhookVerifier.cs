using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;
using Stripe;
using Stripe.Checkout;

namespace Ledgance.Shared.Infrastructure.Billing {
    /// <summary>
    /// Verifies the provider's payload signature and reduces the event to what the application
    /// acts on. Nothing in the payload is read before the signature verifies.
    /// </summary>
    internal sealed class StripeWebhookVerifier : IBillingWebhookVerifier {
        private readonly StripeSettings _settings;

        public StripeWebhookVerifier(StripeSettings settings) {
            _settings = settings;
        }

        public BillingWebhookEvent Verify(string payload, string signatureHeader) {
            if (string.IsNullOrWhiteSpace(_settings.WebhookSecret)) {
                throw new InvalidOperationException(
                    "No webhook secret is configured, so webhooks cannot be verified.");
            }

            var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader,
                _settings.WebhookSecret);

            return stripeEvent.Type switch {
                "checkout.session.completed" => FromCheckoutSession(stripeEvent),
                "customer.subscription.created" or "customer.subscription.updated" =>
                    FromSubscription(stripeEvent, BillingEventKind.SubscriptionChanged),
                "customer.subscription.deleted" =>
                    FromSubscription(stripeEvent, BillingEventKind.SubscriptionEnded),
                "invoice.paid" or "invoice.payment_failed" => FromInvoice(stripeEvent),
                _ => Ignored(stripeEvent)
            };
        }

        private static BillingWebhookEvent Ignored(Event stripeEvent) =>
            new(stripeEvent.Id, stripeEvent.Type, BillingEventKind.Ignored,
                stripeEvent.Created, null, null, null, null);

        private static BillingWebhookEvent FromCheckoutSession(Event stripeEvent) {
            var session = stripeEvent.Data.Object as Session;
            var (organizationId, module, plan) = ReadMetadata(session?.Metadata);

            return new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type,
                BillingEventKind.SubscriptionChanged, stripeEvent.Created, null,
                organizationId, module, plan, session?.SubscriptionId, session?.CustomerId);
        }

        private static BillingWebhookEvent FromSubscription(Event stripeEvent,
            BillingEventKind kind) {
            var subscription = stripeEvent.Data.Object as Subscription;

            if (subscription is null) {
                return Ignored(stripeEvent);
            }

            var (organizationId, module, plan) = ReadMetadata(subscription.Metadata);

            return new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type, kind,
                stripeEvent.Created, StripeBillingGateway.ToSnapshot(subscription),
                organizationId, module, plan, subscription.Id, subscription.CustomerId);
        }

        private static BillingWebhookEvent FromInvoice(Event stripeEvent) {
            var invoice = stripeEvent.Data.Object as Invoice;

            return new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type,
                BillingEventKind.PaymentStateChanged, stripeEvent.Created, null,
                null, null, null, null, invoice?.CustomerId);
        }

        private static (Guid?, ProductModule?, PlanCode?) ReadMetadata(
            IDictionary<string, string>? metadata) {
            if (metadata is null) {
                return (null, null, null);
            }

            Guid? organizationId =
                metadata.TryGetValue(StripeMetadata.OrganizationId, out var rawOrganization)
                    && Guid.TryParse(rawOrganization, out var parsedOrganization)
                        ? parsedOrganization
                        : null;

            ProductModule? module =
                metadata.TryGetValue(StripeMetadata.Module, out var rawModule)
                    && Enum.TryParse<ProductModule>(rawModule, out var parsedModule)
                        ? parsedModule
                        : null;

            PlanCode? plan =
                metadata.TryGetValue(StripeMetadata.PlanCode, out var rawPlan)
                    && Enum.TryParse<PlanCode>(rawPlan, out var parsedPlan)
                        ? parsedPlan
                        : null;

            return (organizationId, module, plan);
        }
    }
}
