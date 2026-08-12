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
                .With(Organization, ProductModule.Audit, PlanCode.AuditSmall);

            var entitlements = await Service(reader)
                .GetAsync(Organization, ProductModule.Audit, default);

            Assert.Equal(PlanCode.AuditSmall, entitlements.Plan);
            Assert.Equal(90, entitlements.Limit(Entitlements.MaxUsers));
            Assert.True(entitlements.Has(Entitlements.AdvancedReview));
        }

        [Fact]
        public async Task A_canceled_subscription_falls_back_to_the_free_plan() {
            var reader = new FakeSubscriptionReader()
                .With(Organization, ProductModule.Audit, PlanCode.AuditMediumGrowth,
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
        [InlineData(PlanCode.Free, "3", "1", "2")]
        [InlineData(PlanCode.AuditMicro, "15", "30", "75")]
        [InlineData(PlanCode.AuditMicroGrowth, "40", "100", "300")]
        [InlineData(PlanCode.AuditSmall, "90", "250", "800")]
        [InlineData(PlanCode.AuditMedium, "150", "500", "1300")]
        [InlineData(PlanCode.AuditMediumGrowth, "200", "-1", "-1")]
        [InlineData(PlanCode.AuditEnterprise, "-1", "-1", "-1")]
        public void Published_audit_capacity_matches_the_catalogue(PlanCode plan, string users,
            string clients, string engagements) {
            var values = SubscriptionPlanCatalog.For(plan);

            Assert.Equal(users, values[Entitlements.MaxUsers]);
            Assert.Equal(clients, values[Entitlements.MaxClients]);
            Assert.Equal(engagements, values[Entitlements.MaxEngagements]);
        }

        [Theory]
        [InlineData(PlanCode.Free, 5L)]
        [InlineData(PlanCode.AuditMicro, 250L)]
        [InlineData(PlanCode.AuditMicroGrowth, 500L)]
        [InlineData(PlanCode.AuditSmall, 750L)]
        [InlineData(PlanCode.AuditMedium, 2048L)]
        [InlineData(PlanCode.AuditMediumGrowth, 6144L)]
        public void Published_audit_storage_matches_the_catalogue(PlanCode plan, long gigabytes) {
            Assert.Equal((gigabytes * 1024L * 1024 * 1024).ToString(),
                SubscriptionPlanCatalog.For(plan)[Entitlements.StorageBytes]);
        }

        /// <summary>
        /// Capacity and AI capability must never move backwards as a customer pays more, on any
        /// dimension. A single mistyped digit in the catalogue would otherwise sell a downgrade.
        /// </summary>
        [Theory]
        [InlineData(ProductModule.Audit)]
        [InlineData(ProductModule.Accounting)]
        public void Every_step_up_the_plan_ladder_is_an_increase(ProductModule module) {
            var ordered = SubscriptionPlanCatalog.Ordered(module);

            // Only the dimensions this product actually meters: the shared Free plan carries a
            // starter allowance on both products' dimensions, while a paid plan zeroes the ones
            // belonging to the other product.
            string[] metered = module == ProductModule.Audit
                ? [Entitlements.MaxUsers, Entitlements.MaxClients,
                   Entitlements.MaxEngagements, Entitlements.StorageBytes,
                   Entitlements.AiMonthlyUnits]
                : [Entitlements.MaxUsers, Entitlements.MaxEntities,
                   Entitlements.MaxTransactionsPerPeriod, Entitlements.StorageBytes,
                   Entitlements.AiMonthlyUnits];

            foreach (var (lower, higher) in ordered.Zip(ordered.Skip(1))) {
                var below = SubscriptionPlanCatalog.For(lower);
                var above = SubscriptionPlanCatalog.For(higher);

                foreach (var key in metered) {
                    var lowerValue = long.Parse(below[key]);
                    var higherValue = long.Parse(above[key]);

                    Assert.True(higherValue == EntitlementSet.Unlimited
                        || (lowerValue != EntitlementSet.Unlimited && higherValue >= lowerValue),
                        $"{higher} offers less {key} ({higherValue}) than {lower} ({lowerValue}).");
                }

                Assert.True(AiTiers.RankOf(above[Entitlements.AiMaxTier])
                    >= AiTiers.RankOf(below[Entitlements.AiMaxTier]),
                    $"{higher} has a lower AI tier than {lower}.");

                Assert.True(AiReportScopes.RankOf(above[Entitlements.AiReportScope])
                    >= AiReportScopes.RankOf(below[Entitlements.AiReportScope]),
                    $"{higher} generates less of a report than {lower}.");

                Assert.True(AiAnalysisScopes.RankOf(above[Entitlements.AiAnalysisScope])
                    >= AiAnalysisScopes.RankOf(below[Entitlements.AiAnalysisScope]),
                    $"{higher} analyses less than {lower}.");
            }
        }

        [Fact]
        public void The_next_plan_up_stays_inside_the_same_product() {
            Assert.Equal(PlanCode.AuditMicroGrowth,
                SubscriptionPlanCatalog.NextAbove(PlanCode.AuditMicro));
            Assert.Equal(PlanCode.AuditEnterprise,
                SubscriptionPlanCatalog.NextAbove(PlanCode.AuditMediumGrowth));
            Assert.Null(SubscriptionPlanCatalog.NextAbove(PlanCode.AuditEnterprise));
            Assert.Null(SubscriptionPlanCatalog.NextAbove(PlanCode.AccountingEnterprise));
        }

        [Fact]
        public void Only_enterprise_plans_require_contact_sales() {
            Assert.True(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AuditEnterprise));
            Assert.True(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AccountingEnterprise));
            Assert.False(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.AuditMediumGrowth));
            Assert.False(SubscriptionPlanCatalog.RequiresContactSales(PlanCode.Free));
        }

        /// <summary>
        /// A stored plan string that no longer maps to a member — a retired plan code, or a
        /// tampered row — must resolve to Free rather than to whatever the string resembles.
        /// </summary>
        [Fact]
        public void A_retired_plan_code_resolves_to_free() {
            Assert.False(Enum.TryParse<PlanCode>("AuditProfessional", ignoreCase: true,
                out var retired) && Enum.IsDefined(retired));
        }
    }
}
