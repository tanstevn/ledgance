using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Shared.Unit.Tests.Subscriptions {
    public class SubscriptionPlansQueryTests {
        private static GetSubscriptionPlansQueryHandler Handler(
            StubPriceCatalog? catalog = null, StubPriceReader? prices = null) =>
            new(catalog ?? new StubPriceCatalog(), prices ?? new StubPriceReader());

        [Fact]
        public async Task The_public_catalog_exposes_every_plan_with_its_entitlements() {
            var result = await Handler()
                .HandleAsync(new GetSubscriptionPlansQuery(), CancellationToken.None);

            Assert.True(result.Successful);
            var plans = result.Data!.ToDictionary(plan => plan.Code);

            Assert.Equal(SubscriptionPlanCatalog.All.Count, plans.Count);

            Assert.True(plans["Free"].IsFree);
            Assert.False(plans["Free"].RequiresContactSales);

            Assert.Equal("Accounting", plans["AccountingSolo"].Module);
            Assert.Equal("Audit", plans["AuditProfessional"].Module);

            Assert.True(plans["AuditEnterprise"].RequiresContactSales);
            Assert.True(plans["AccountingEnterprise"].RequiresContactSales);

            Assert.Equal("3", plans["AccountingSolo"].Entitlements[Entitlements.MaxEntities]);
            Assert.Equal("30",
                plans["AuditProfessional"].Entitlements[Entitlements.MaxUsers]);
        }

        [Fact]
        public async Task Only_a_paid_plan_with_a_configured_price_is_offered_for_purchase() {
            var prices = new StubPriceCatalog().With(PlanCode.AccountingSolo, "price_solo");

            var result = await Handler(prices)
                .HandleAsync(new GetSubscriptionPlansQuery(), CancellationToken.None);

            var plans = result.Data!.ToDictionary(plan => plan.Code);

            Assert.True(plans["AccountingSolo"].Purchasable);
            Assert.False(plans["Free"].Purchasable);
            Assert.False(plans["AccountingEnterprise"].Purchasable);
            Assert.False(plans["AuditProfessional"].Purchasable);
        }

        [Fact]
        public async Task A_plan_carries_the_live_price_the_provider_will_charge() {
            var catalog = new StubPriceCatalog().With(PlanCode.AccountingSolo, "price_solo");
            var prices = new StubPriceReader().With(PlanCode.AccountingSolo, 1499, "USD");

            var result = await Handler(catalog, prices)
                .HandleAsync(new GetSubscriptionPlansQuery(), CancellationToken.None);

            var plans = result.Data!.ToDictionary(plan => plan.Code);
            var solo = plans["AccountingSolo"];

            Assert.Equal(1499, solo.AmountMinorUnits);
            Assert.Equal("USD", solo.Currency);
            Assert.Equal("month", solo.Interval);
            Assert.Equal(1, solo.IntervalCount);

            Assert.Null(plans["AccountingTeam"].AmountMinorUnits);
            Assert.Null(plans["Free"].AmountMinorUnits);
        }

        [Fact]
        public async Task A_configured_price_the_provider_cannot_report_still_renders_the_page() {
            var catalog = new StubPriceCatalog().With(PlanCode.AccountingSolo, "price_solo");

            var result = await Handler(catalog)
                .HandleAsync(new GetSubscriptionPlansQuery(), CancellationToken.None);

            var solo = result.Data!.Single(plan => plan.Code == "AccountingSolo");

            Assert.True(result.Successful);
            Assert.True(solo.Purchasable);
            Assert.Null(solo.AmountMinorUnits);
        }

        [Fact]
        public void The_plans_query_is_anonymous_by_design() =>
            Assert.NotEmpty(typeof(GetSubscriptionPlansQuery).GetCustomAttributes(
                typeof(Shared.Application.Authorization.AllowAnonymousRequestAttribute),
                inherit: false));
    }
}
