using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Billing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ledgance.Shared.Unit.Tests.Billing {
    /// <summary>
    /// Configuration is where a plan becomes purchasable, so a malformed entry must make the
    /// plan unavailable rather than reach the provider and fail at checkout.
    /// </summary>
    public class BillingPriceCatalogTests {
        private static ConfiguredBillingPriceCatalog Catalog(
            params (string Code, string Value)[] prices) =>
            new(new StripeSettings {
                Prices = prices.ToDictionary(entry => entry.Code, entry => entry.Value)
            }, NullLogger<ConfiguredBillingPriceCatalog>.Instance);

        [Fact]
        public void A_real_price_id_maps_both_ways() {
            var catalog = Catalog(("AccountingSolo", "price_1QxSolo"));

            Assert.Equal("price_1QxSolo", catalog.PriceIdFor(PlanCode.AccountingSolo));
            Assert.Equal(PlanCode.AccountingSolo, catalog.PlanForPriceId("price_1QxSolo"));
        }

        [Theory]
        [InlineData("10")]
        [InlineData("14.99")]
        [InlineData("prod_1QxSolo")]
        [InlineData("price_replace_me")]
        [InlineData("")]
        public void Anything_that_is_not_a_price_id_leaves_the_plan_unpurchasable(string value) {
            var catalog = Catalog(("AccountingSolo", value));

            Assert.Null(catalog.PriceIdFor(PlanCode.AccountingSolo));
        }

        [Fact]
        public void An_entry_for_an_unknown_plan_is_ignored() {
            var catalog = Catalog(("NotAPlan", "price_1Qx"), ("AccountingSolo", "price_1QxSolo"));

            Assert.Null(catalog.PlanForPriceId("price_1Qx"));
            Assert.Equal("price_1QxSolo", catalog.PriceIdFor(PlanCode.AccountingSolo));
        }
    }
}
