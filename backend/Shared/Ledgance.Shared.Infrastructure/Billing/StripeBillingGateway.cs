using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;
using Stripe;
using Stripe.Checkout;
using PortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using PortalSessionService = Stripe.BillingPortal.SessionService;

namespace Ledgance.Shared.Infrastructure.Billing {
    internal static class StripeMetadata {
        public const string OrganizationId = "ledgance_organization_id";
        public const string Module = "ledgance_module";
        public const string PlanCode = "ledgance_plan_code";
    }

    /// <summary>
    /// The only place Stripe types appear. Everything above this adapter speaks the
    /// application's own billing vocabulary, so the provider can change without touching a
    /// slice.
    /// </summary>
    internal sealed class StripeBillingGateway : IBillingGateway {
        private readonly StripeSettings _settings;
        private readonly CustomerService _customers;
        private readonly SessionService _checkoutSessions;
        private readonly PortalSessionService _portalSessions;
        private readonly SubscriptionService _subscriptions;

        public StripeBillingGateway(StripeSettings settings) {
            _settings = settings;

            var client = new StripeClient(settings.SecretKey);
            _customers = new CustomerService(client);
            _checkoutSessions = new SessionService(client);
            _portalSessions = new PortalSessionService(client);
            _subscriptions = new SubscriptionService(client);
        }

        public async Task<string> EnsureCustomerAsync(Guid organizationId,
            string organizationName, string email, string? existingCustomerId,
            CancellationToken ct) {
            if (!string.IsNullOrWhiteSpace(existingCustomerId)) {
                return existingCustomerId;
            }

            var customer = await _customers.CreateAsync(new CustomerCreateOptions {
                Name = organizationName,
                Email = email,
                Metadata = new Dictionary<string, string> {
                    [StripeMetadata.OrganizationId] = organizationId.ToString()
                }
            }, cancellationToken: ct);

            return customer.Id;
        }

        public async Task<string> CreateCheckoutSessionAsync(CheckoutRequest request,
            CancellationToken ct) {
            var metadata = new Dictionary<string, string> {
                [StripeMetadata.OrganizationId] = request.OrganizationId.ToString(),
                [StripeMetadata.Module] = request.Module.ToString(),
                [StripeMetadata.PlanCode] = request.Plan.ToString()
            };

            var options = new SessionCreateOptions {
                Mode = "subscription",
                Customer = request.CustomerId,
                ClientReferenceId = request.OrganizationId.ToString(),
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems = [
                    new SessionLineItemOptions { Price = request.PriceId, Quantity = 1 }
                ],
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                AllowPromotionCodes = true,
                BillingAddressCollection = "auto",
                CustomerUpdate = new SessionCustomerUpdateOptions {
                    Address = "auto",
                    Name = "auto"
                }
            };

            if (_settings.PaymentMethodTypes.Count > 0) {
                options.PaymentMethodTypes = _settings.PaymentMethodTypes;
            }

            var session = await _checkoutSessions.CreateAsync(options, cancellationToken: ct);

            return session.Url;
        }

        public async Task<string> CreateBillingPortalSessionAsync(string customerId,
            string returnUrl, CancellationToken ct) {
            var session = await _portalSessions.CreateAsync(new PortalSessionCreateOptions {
                Customer = customerId,
                ReturnUrl = returnUrl
            }, cancellationToken: ct);

            return session.Url;
        }

        public async Task<BillingSubscriptionSnapshot> ChangePlanAsync(string subscriptionId,
            string priceId, CancellationToken ct) {
            var subscription = await _subscriptions.GetAsync(subscriptionId,
                cancellationToken: ct);

            var item = subscription.Items.Data.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"Subscription '{subscriptionId}' has no billable item.");

            var updated = await _subscriptions.UpdateAsync(subscriptionId,
                new SubscriptionUpdateOptions {
                    CancelAtPeriodEnd = false,
                    ProrationBehavior = "create_prorations",
                    Items = [
                        new SubscriptionItemOptions { Id = item.Id, Price = priceId }
                    ]
                }, cancellationToken: ct);

            return ToSnapshot(updated);
        }

        public async Task<BillingSubscriptionSnapshot> SetCancellationAsync(string subscriptionId,
            bool cancelAtPeriodEnd, CancellationToken ct) {
            var updated = await _subscriptions.UpdateAsync(subscriptionId,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd },
                cancellationToken: ct);

            return ToSnapshot(updated);
        }

        public async Task<BillingSubscriptionSnapshot?> GetSubscriptionAsync(
            string subscriptionId, CancellationToken ct) {
            try {
                var subscription = await _subscriptions.GetAsync(subscriptionId,
                    cancellationToken: ct);

                return ToSnapshot(subscription);
            }
            catch (StripeException) {
                return null;
            }
        }

        internal static BillingSubscriptionSnapshot ToSnapshot(Subscription subscription) {
            var item = subscription.Items?.Data?.FirstOrDefault();

            return new BillingSubscriptionSnapshot(
                subscription.Id,
                subscription.CustomerId,
                item?.Price?.Id,
                MapStatus(subscription.Status),
                item?.CurrentPeriodEnd,
                subscription.CancelAtPeriodEnd);
        }

        internal static SubscriptionStatus MapStatus(string status) => status switch {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" or "unpaid" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.Canceled
        };
    }
}
