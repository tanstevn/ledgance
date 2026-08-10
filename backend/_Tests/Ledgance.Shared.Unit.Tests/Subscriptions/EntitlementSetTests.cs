using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Unit.Tests.Subscriptions {
    public class EntitlementSetTests {
        private static EntitlementSet Set(params (string Key, string Value)[] values) =>
            new(ProductModule.Audit, PlanCode.Free,
                values.ToDictionary(entry => entry.Key, entry => entry.Value));

        [Fact]
        public void Has_reads_boolean_capabilities() {
            var entitlements = Set((Entitlements.AdvancedAnalysis, "true"),
                (Entitlements.Automation, "false"));

            Assert.True(entitlements.Has(Entitlements.AdvancedAnalysis));
            Assert.False(entitlements.Has(Entitlements.Automation));
            Assert.False(entitlements.Has(Entitlements.ApiAccess));
        }

        [Fact]
        public void Unknown_limits_are_zero_rather_than_unbounded() {
            Assert.Equal(0, Set().Limit(Entitlements.MaxUsers));
            Assert.False(Set().IsWithinLimit(Entitlements.MaxUsers, 1));
        }

        [Fact]
        public void Unlimited_accepts_any_total() {
            var entitlements = Set((Entitlements.MaxClients, "-1"));

            Assert.True(entitlements.IsWithinLimit(Entitlements.MaxClients, long.MaxValue));
        }

        [Fact]
        public void A_total_equal_to_the_limit_is_still_within_it() {
            var entitlements = Set((Entitlements.MaxUsers, "30"));

            Assert.True(entitlements.IsWithinLimit(Entitlements.MaxUsers, 30));
            Assert.False(entitlements.IsWithinLimit(Entitlements.MaxUsers, 31));
        }

        [Fact]
        public void RequireCapability_reports_the_missing_entitlement() {
            var exception = Assert.Throws<EntitlementException>(
                () => Set().RequireCapability(Entitlements.AdvancedReview));

            Assert.Contains(Entitlements.AdvancedReview, exception.Message);
        }

        [Fact]
        public void RequireWithinLimit_reports_the_configured_limit() {
            var entitlements = Set((Entitlements.MaxEngagements, "3"));

            var exception = Assert.Throws<EntitlementException>(
                () => entitlements.RequireWithinLimit(Entitlements.MaxEngagements, 4));

            Assert.Contains("3", exception.Message);
        }

        [Theory]
        [InlineData(AiTiers.Basic, AiTiers.Basic, true)]
        [InlineData(AiTiers.Advanced, AiTiers.Basic, true)]
        [InlineData(AiTiers.Basic, AiTiers.Reasoning, false)]
        [InlineData(AiTiers.Reasoning, AiTiers.Agentic, false)]
        public void Ai_tiers_are_ordered(string permitted, string requested, bool allowed) {
            Assert.Equal(allowed, AiTiers.Allows(permitted, requested));
        }
    }
}
