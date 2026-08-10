using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Unit.Tests.Subscriptions {
    public class SubscriptionPlansQueryTests {
        [Fact]
        public async Task The_public_catalog_exposes_every_plan_with_its_entitlements() {
            var result = await new GetSubscriptionPlansQueryHandler()
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
        public void The_plans_query_is_anonymous_by_design() =>
            Assert.NotEmpty(typeof(GetSubscriptionPlansQuery).GetCustomAttributes(
                typeof(Shared.Application.Authorization.AllowAnonymousRequestAttribute),
                inherit: false));
    }
}
