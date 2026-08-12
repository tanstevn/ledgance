using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Ledgance.Shared.Infrastructure.Billing {
    public sealed class StripeSettings {
        public const string SectionName = "Stripe";

        public string SecretKey { get; set; } = string.Empty;

        /// <summary>Safe to expose to a browser; the frontend reads it from its own environment.</summary>
        public string PublishableKey { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>Plan code to price identifier, e.g. "AccountingSolo": "price_123".</summary>
        public Dictionary<string, string> Prices { get; set; } = [];

        /// <summary>
        /// Leave empty so the provider offers whatever the account has enabled for the
        /// customer's country — card, wallets, and local methods such as GCash and Maya where
        /// the account supports them. Naming methods here pins the list instead.
        /// </summary>
        public List<string> PaymentMethodTypes { get; set; } = [];

        public string CheckoutSuccessUrl { get; set; } =
            "http://localhost:3000/subscribe/success";

        public string CheckoutCancelUrl { get; set; } = "http://localhost:3000/pricing";

        public string PortalReturnUrl { get; set; } = "http://localhost:3000/dashboard/billing";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SecretKey) && !SecretKey.Contains("replace", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class ConfiguredBillingUrls : IBillingUrls {
        private readonly StripeSettings _settings;

        public ConfiguredBillingUrls(StripeSettings settings) {
            _settings = settings;
        }

        public string CheckoutSuccessUrl => _settings.CheckoutSuccessUrl;
        public string CheckoutCancelUrl => _settings.CheckoutCancelUrl;
        public string PortalReturnUrl => _settings.PortalReturnUrl;
    }

    /// <summary>
    /// Price identifiers live in configuration, so adding or repricing a plan is a settings
    /// change. A plan with no configured price cannot be purchased at all.
    /// </summary>
    public sealed class ConfiguredBillingPriceCatalog : IBillingPriceCatalog {
        private const string PriceIdPrefix = "price_";

        private readonly Dictionary<PlanCode, string> _byPlan = [];
        private readonly Dictionary<string, PlanCode> _byPrice = [];

        public ConfiguredBillingPriceCatalog(StripeSettings settings,
            ILogger<ConfiguredBillingPriceCatalog> logger) {
            foreach (var (code, priceId) in settings.Prices) {
                if (!Enum.TryParse<PlanCode>(code, ignoreCase: true, out var plan)) {
                    logger.LogWarning(
                        "Stripe:Prices has an entry for '{Code}', which is not a plan code.",
                        code);

                    continue;
                }

                if (string.IsNullOrWhiteSpace(priceId)
                    || priceId.Contains("replace", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!priceId.StartsWith(PriceIdPrefix, StringComparison.Ordinal)) {
                    logger.LogWarning(
                        "Stripe:Prices:{Code} is '{Value}', which is not a Stripe price id " +
                        "(expected '{Prefix}…'). {Code} stays unavailable for purchase.",
                        code, priceId, PriceIdPrefix, code);

                    continue;
                }

                _byPlan[plan] = priceId;
                _byPrice[priceId] = plan;
            }
        }

        public string? PriceIdFor(PlanCode plan) =>
            _byPlan.GetValueOrDefault(plan);

        public PlanCode? PlanForPriceId(string priceId) =>
            _byPrice.TryGetValue(priceId, out var plan) ? plan : null;
    }
}
