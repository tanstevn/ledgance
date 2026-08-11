using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Shared.Application.Subscriptions {
    /// <summary>
    /// The public plan catalog: anonymous because pricing pages render before sign-in. It
    /// exposes only what the catalog already declares — entitlement values, never secrets —
    /// so marketing surfaces and the application stay consistent with the same source of
    /// truth the backend enforces.
    /// </summary>
    [AllowAnonymousRequest]
    public class GetSubscriptionPlansQuery
        : IQuery<Result<IEnumerable<SubscriptionPlanRow>>> { }

    public class SubscriptionPlanRow {
        public string Code { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public bool RequiresContactSales { get; set; }

        /// <summary>
        /// Whether this plan can actually be bought right now — a paid plan with a price
        /// configured on the payment provider. The UI offers checkout only for these, so it
        /// never promises a purchase the server would refuse.
        /// </summary>
        public bool Purchasable { get; set; }

        /// <summary>
        /// The live price the payment provider will charge, in the currency's smallest unit.
        /// Null when the plan has no price yet — pricing surfaces say so instead of guessing.
        /// </summary>
        public long? AmountMinorUnits { get; set; }

        public string? Currency { get; set; }

        /// <summary>Billing interval as the provider reports it: "month" or "year".</summary>
        public string? Interval { get; set; }

        public int? IntervalCount { get; set; }

        public Dictionary<string, string> Entitlements { get; set; } = [];
    }

    public class GetSubscriptionPlansQueryHandler
        : IRequestHandler<GetSubscriptionPlansQuery, Result<IEnumerable<SubscriptionPlanRow>>> {
        private readonly IBillingPriceCatalog _catalog;
        private readonly IBillingPriceReader _prices;

        public GetSubscriptionPlansQueryHandler(IBillingPriceCatalog catalog,
            IBillingPriceReader prices) {
            _catalog = catalog;
            _prices = prices;
        }

        public async Task<Result<IEnumerable<SubscriptionPlanRow>>> HandleAsync(
            GetSubscriptionPlansQuery request, CancellationToken ct) {
            var prices = await _prices.GetPricesAsync(ct);

            return Result<IEnumerable<SubscriptionPlanRow>>.Success(
                SubscriptionPlanCatalog.All.Select(plan => {
                    var price = prices.GetValueOrDefault(plan.Key);

                    return new SubscriptionPlanRow {
                        Code = plan.Key.ToString(),
                        Module = SubscriptionPlanCatalog.ModuleOf(plan.Key).ToString(),
                        IsFree = plan.Key == PlanCode.Free,
                        RequiresContactSales =
                            SubscriptionPlanCatalog.RequiresContactSales(plan.Key),
                        Purchasable = plan.Key != PlanCode.Free
                            && !SubscriptionPlanCatalog.RequiresContactSales(plan.Key)
                            && _catalog.PriceIdFor(plan.Key) is not null,
                        AmountMinorUnits = price?.AmountMinorUnits,
                        Currency = price?.Currency,
                        Interval = price?.Interval,
                        IntervalCount = price?.IntervalCount,
                        Entitlements = plan.Value.ToDictionary(entry => entry.Key,
                            entry => entry.Value)
                    };
                }));
        }
    }
}
