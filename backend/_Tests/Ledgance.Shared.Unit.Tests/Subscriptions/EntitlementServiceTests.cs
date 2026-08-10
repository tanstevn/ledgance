using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Subscriptions;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace Ledgance.Shared.Unit.Tests.Subscriptions {
    public class EntitlementServiceTests {
        private static readonly Guid Organization = TestIdentity.DefaultOrganizationId;

        private static IEntitlementService Service(ISubscriptionReader reader,
            SubscriptionSettings? settings = null) =>
            new EntitlementService(reader, Options.Create(settings ?? new SubscriptionSettings()));

        [Fact]
        public async Task An_organization_without_a_subscription_gets_the_free_plan() {
            var entitlements = await Service(new FakeSubscriptionReader())
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(PlanCode.Free, entitlements.Plan);
            Assert.True(entitlements.Has(Entitlements.AiEnabled));
        }

        [Fact]
        public async Task A_paid_plan_supplies_its_catalogued_limits() {
            var reader = new FakeSubscriptionReader()
                .With(Organization, ProductModule.Audit, PlanCode.AuditOrganization);

            var entitlements = await Service(reader)
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(PlanCode.AuditOrganization, entitlements.Plan);
            Assert.Equal(75, entitlements.Limit(Entitlements.MaxUsers));
            Assert.True(entitlements.Has(Entitlements.AdvancedReview));
        }

        [Fact]
        public async Task A_canceled_subscription_falls_back_to_the_free_plan() {
            var reader = new FakeSubscriptionReader()
                .With(Organization, ProductModule.Audit, PlanCode.AuditFirm,
                    SubscriptionStatus.Canceled);

            var entitlements = await Service(reader)
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(PlanCode.Free, entitlements.Plan);
            Assert.False(entitlements.Has(Entitlements.AdvancedReview));
        }

        [Fact]
        public async Task A_trialing_subscription_keeps_its_paid_plan() {
            var reader = new FakeSubscriptionReader()
                .With(Organization, ProductModule.Accounting, PlanCode.AccountingTeam,
                    SubscriptionStatus.Trialing);

            var entitlements = await Service(reader)
                .GetAsync(Organization, ProductModule.Accounting, default);

            Assert.Equal(PlanCode.AccountingTeam, entitlements.Plan);
        }

        [Fact]
        public async Task Configuration_overrides_the_catalogued_value() {
            var settings = new SubscriptionSettings {
                Plans = {
                    [nameof(PlanCode.Free)] = new Dictionary<string, string> {
                        [Entitlements.MaxUsers] = "5"
                    }
                }
            };

            var entitlements = await Service(new FakeSubscriptionReader(), settings)
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(5, entitlements.Limit(Entitlements.MaxUsers));
        }

        [Fact]
        public async Task A_negotiated_organization_override_wins_over_configuration() {
            var settings = new SubscriptionSettings {
                Plans = {
                    [nameof(PlanCode.AuditEnterprise)] = new Dictionary<string, string> {
                        [Entitlements.MaxUsers] = "500"
                    }
                }
            };

            var reader = new FakeSubscriptionReader()
                .With(Organization, ProductModule.Audit, PlanCode.AuditEnterprise,
                    overrides: new Dictionary<string, string> {
                        [Entitlements.MaxUsers] = "900"
                    });

            var entitlements = await Service(reader, settings)
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(900, entitlements.Limit(Entitlements.MaxUsers));
        }

        [Theory]
        [InlineData(PlanCode.AuditProfessional, 30)]
        [InlineData(PlanCode.AuditOrganization, 75)]
        [InlineData(PlanCode.AuditFirm, 150)]
        public void Published_audit_user_caps_match_the_catalogue(PlanCode plan, long users) {
            Assert.Equal(users.ToString(),
                SubscriptionPlanCatalog.For(plan)[Entitlements.MaxUsers]);
        }

        [Fact]
        public void Only_enterprise_plans_require_contact_sales() {
            Assert.True(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AuditEnterprise));
            Assert.True(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AccountingEnterprise));
            Assert.False(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AuditFirm));
            Assert.False(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.Free));
        }
    }
}
