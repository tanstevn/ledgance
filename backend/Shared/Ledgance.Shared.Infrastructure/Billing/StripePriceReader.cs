using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Ledgance.Shared.Infrastructure.Billing {
    /// <summary>
    /// Reads the configured plans' prices from Stripe so pricing surfaces show what the customer
    /// will actually be charged. Results are cached for a few minutes: the plans endpoint is
    /// anonymous and public, and a price changes far less often than the page is loaded. A
    /// failure is logged and answered with no prices, which renders as "not priced yet" rather
    /// than taking the pricing page down.
    /// </summary>
    internal sealed class StripePriceReader : IBillingPriceReader {
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        private readonly IBillingPriceCatalog _catalog;
        private readonly StripeSettings _settings;
        private readonly PriceService _prices;
        private readonly ILogger<StripePriceReader> _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private IReadOnlyDictionary<PlanCode, BillingPrice> _cached =
            new Dictionary<PlanCode, BillingPrice>();
        private DateTime _cachedAt = DateTime.MinValue;

        public StripePriceReader(IBillingPriceCatalog catalog, StripeSettings settings,
            ILogger<StripePriceReader> logger) {
            _catalog = catalog;
            _settings = settings;
            _prices = new PriceService(new StripeClient(settings.SecretKey));
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<PlanCode, BillingPrice>> GetPricesAsync(
            CancellationToken ct) {
            if (!_settings.IsConfigured) {
                return _cached;
            }

            if (DateTime.UtcNow - _cachedAt < CacheLifetime) {
                return _cached;
            }

            await _refreshLock.WaitAsync(ct);

            try {
                if (DateTime.UtcNow - _cachedAt < CacheLifetime) {
                    return _cached;
                }

                var resolved = new Dictionary<PlanCode, BillingPrice>();

                foreach (var plan in Enum.GetValues<PlanCode>()) {
                    var priceId = _catalog.PriceIdFor(plan);

                    if (priceId is null) {
                        continue;
                    }

                    var price = await ReadAsync(plan, priceId, ct);

                    if (price is not null) {
                        resolved[plan] = price;
                    }
                }

                _cached = resolved;
                _cachedAt = DateTime.UtcNow;

                return _cached;
            }
            finally {
                _refreshLock.Release();
            }
        }

        private async Task<BillingPrice?> ReadAsync(PlanCode plan, string priceId,
            CancellationToken ct) {
            try {
                var price = await _prices.GetAsync(priceId, cancellationToken: ct);

                if (price.UnitAmount is null || price.Recurring is null) {
                    _logger.LogWarning(
                        "Price {PriceId} for {Plan} is not a recurring amount and is ignored.",
                        priceId, plan);

                    return null;
                }

                return new BillingPrice(price.Id, price.UnitAmount.Value,
                    price.Currency.ToUpperInvariant(), price.Recurring.Interval,
                    (int)price.Recurring.IntervalCount);
            }
            catch (StripeException exception) {
                // A misconfigured id must not take the pricing page down; the plan simply shows
                // as unpriced until the configuration is corrected.
                _logger.LogWarning("Could not read price {PriceId} for {Plan}: {Reason}",
                    priceId, plan, exception.Message);

                return null;
            }
        }
    }
}
