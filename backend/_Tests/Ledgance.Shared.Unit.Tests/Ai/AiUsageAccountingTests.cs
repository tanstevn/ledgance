using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ledgance.Shared.Unit.Tests.Ai {
    /// <summary>
    /// How AI usage is priced, taken and given back. The reservation happens before the work
    /// runs, so these tests are also what proves two simultaneous callers cannot spend the same
    /// remaining credits.
    /// </summary>
    public class AiUsageAccountingTests {
        private static readonly Guid Organization = TestIdentity.DefaultOrganizationId;

        private readonly FakeCurrentUserAccessor _user = new(TestIdentity.User());
        private readonly FakeEntitlementService _entitlements = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly FakeAiChatClient _ollama = new(AiProviders.Ollama);
        private readonly FakeAiChatClient _openAi = new(AiProviders.OpenAI);

        private AiCompletionService Service() =>
            new(_user, _entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_ollama, _openAi], NullLogger<AiCompletionService>.Instance);

        private static AiWorkload Workload(long cost, string tier = AiTiers.Basic,
            string capability = "audit.test") =>
            AiWorkload.For(ProductModule.Audit, capability, tier, "system", "prompt",
                cost: cost);

        [Fact]
        public async Task An_operation_consumes_the_units_it_declares() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            var completion = await Service().CompleteAsync(Workload(7), default);

            Assert.Equal(7, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Equal(7, completion.Usage!.UnitsConsumed);
            Assert.Equal(193, completion.Usage.UnitsRemaining);
        }

        [Fact]
        public async Task Expensive_operations_cost_more_than_cheap_ones() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth);

            await Service().CompleteAsync(Workload(1, capability: "audit.assistant"), default);
            var afterCheap = _usage.UsedNow(Organization, ProductModule.Audit);

            await Service().CompleteAsync(Workload(80, capability: "audit.agentic_report"),
                default);
            var afterExpensive = _usage.UsedNow(Organization, ProductModule.Audit);

            Assert.Equal(1, afterCheap);
            Assert.Equal(81, afterExpensive);
        }

        [Fact]
        public async Task Configuration_can_reprice_an_operation_without_touching_code() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _costs.Overrides["audit.test"] = 25;

            await Service().CompleteAsync(Workload(1), default);

            Assert.Equal(25, _usage.UsedNow(Organization, ProductModule.Audit));
        }

        [Fact]
        public async Task An_operation_costing_more_than_the_remainder_is_refused_outright() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _usage.Seed(Organization, ProductModule.Audit, 195);

            var exception = await Assert.ThrowsAsync<AiUsageLimitException>(
                () => Service().CompleteAsync(Workload(10), default));

            Assert.Contains("10 AI credits", exception.Message);
            Assert.Empty(_ollama.Calls);
            Assert.Equal(195, _usage.UsedNow(Organization, ProductModule.Audit));
        }

        /// <summary>
        /// The refusal has to be actionable on its own: what is left, when it refills, and what
        /// the next plan carries — with no provider, model or internal detail in it.
        /// </summary>
        [Fact]
        public async Task The_refusal_explains_the_reset_and_the_upgrade() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMicro);
            _periods.Period = new AiUsagePeriod("sub:2026-09-14",
                new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
            _usage.Seed(Organization, ProductModule.Audit, 12_000, "sub:2026-09-14");

            var exception = await Assert.ThrowsAsync<AiUsageLimitException>(
                () => Service().CompleteAsync(Workload(6, AiTiers.Advanced), default));

            Assert.Contains("14 September 2026", exception.Message);
            Assert.Contains("AuditMicroGrowth", exception.Message);
            Assert.Contains("40,000", exception.Message);
            Assert.DoesNotContain("Ollama", exception.Message);
            Assert.DoesNotContain("Exception", exception.Message);
        }

        [Fact]
        public async Task An_unlimited_plan_is_never_refused_but_usage_is_still_recorded() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditEnterprise);
            _usage.Seed(Organization, ProductModule.Audit, 10_000_000);

            var completion = await Service()
                .CompleteAsync(Workload(500, AiTiers.Agentic), default);

            Assert.True(completion.Usage!.IsUnlimited);
            Assert.Equal(10_000_500, _usage.UsedNow(Organization, ProductModule.Audit));
        }

        /// <summary>
        /// Enterprise capacity is negotiated, and a per-organization override is how that is
        /// expressed — no arbitrary fixed ceiling is imposed in code.
        /// </summary>
        [Fact]
        public async Task A_negotiated_enterprise_allowance_replaces_unlimited() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditEnterprise,
                new Dictionary<string, string> {
                    [Entitlements.AiMonthlyUnits] = "500"
                });
            _usage.Seed(Organization, ProductModule.Audit, 480);

            await Assert.ThrowsAsync<AiUsageLimitException>(
                () => Service().CompleteAsync(Workload(40, AiTiers.Agentic), default));
        }

        [Fact]
        public async Task Work_that_never_reached_a_provider_gives_its_units_back() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _ollama.Throws = new HttpRequestException("provider down");

            await Assert.ThrowsAsync<AiUnavailableException>(
                () => Service().CompleteAsync(Workload(5), default));

            Assert.Equal(0, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Single(_usage.Released);
        }

        [Fact]
        public async Task A_capability_the_plan_does_not_include_costs_nothing() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            await Assert.ThrowsAsync<EntitlementException>(
                () => Service().CompleteAsync(Workload(50, AiTiers.Reasoning), default));

            Assert.Equal(0, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Empty(_usage.Reserved);
        }

        /// <summary>
        /// The failure this whole design exists to prevent: several callers reading the same
        /// remaining balance and each deciding there is room.
        /// </summary>
        [Fact]
        public async Task Concurrent_operations_cannot_spend_the_same_remaining_credits() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _usage.Seed(Organization, ProductModule.Audit, 190);

            var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ => {
                try {
                    await Service().CompleteAsync(Workload(5), default);
                    return true;
                }
                catch (AiUsageLimitException) {
                    return false;
                }
            }));

            Assert.Equal(2, attempts.Count(granted => granted));
            Assert.Equal(200, _usage.UsedNow(Organization, ProductModule.Audit));
        }

        [Fact]
        public async Task Usage_is_charged_to_the_calling_organization_and_user() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            await Service().CompleteAsync(AiWorkload.For(ProductModule.Audit,
                "audit.report_section", AiTiers.Basic, "system", "prompt", null,
                AiReportScopes.None, AiAnalysisScopes.Document, 3,
                clientId: null, engagementId: TestIdentity.DefaultOrganizationId), default);

            var charged = Assert.Single(_usage.Reserved);

            Assert.Equal(Organization, charged.OrganizationId);
            Assert.Equal(TestIdentity.User().UserId, charged.UserId);
            Assert.Equal(ProductModule.Audit, charged.Module);
            Assert.Equal("audit.report_section", charged.Capability);
            Assert.Equal(TestIdentity.DefaultOrganizationId, charged.EngagementId);
        }

        /// <summary>
        /// A new period is a new allowance. Usage does not accumulate forever, and the previous
        /// period's total is left where it was rather than being reset in place.
        /// </summary>
        [Fact]
        public async Task A_new_period_starts_from_an_empty_allowance() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _periods.Period = new AiUsagePeriod("sub:2026-08-14", null);
            _usage.Seed(Organization, ProductModule.Audit, 200, "sub:2026-08-14");

            await Assert.ThrowsAsync<AiUsageLimitException>(
                () => Service().CompleteAsync(Workload(1), default));

            _periods.Period = new AiUsagePeriod("sub:2026-09-14", null);

            var completion = await Service().CompleteAsync(Workload(1), default);

            Assert.Equal(1, completion.Usage!.UnitsConsumed);
            Assert.Equal(1, _usage.UsedNow(Organization, ProductModule.Audit, "sub:2026-09-14"));
            Assert.Equal(200, _usage.UsedNow(Organization, ProductModule.Audit, "sub:2026-08-14"));
        }

        [Fact]
        public async Task Approaching_the_limit_is_reported_before_the_limit_is_reached() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _usage.Seed(Organization, ProductModule.Audit, 100);

            var comfortable = await Service().CompleteAsync(Workload(1), default);
            Assert.False(comfortable.Usage!.IsApproachingLimit);

            _usage.Seed(Organization, ProductModule.Audit, 165);

            var tight = await Service().CompleteAsync(Workload(1), default);
            Assert.True(tight.Usage!.IsApproachingLimit);
            Assert.Equal(34, tight.Usage.UnitsRemaining);
        }

        /// <summary>
        /// One organization's spending must never show up against another's allowance.
        /// </summary>
        [Fact]
        public async Task Usage_is_isolated_between_organizations() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            var other = Guid.NewGuid();
            _usage.Seed(other, ProductModule.Audit, 199);

            await Service().CompleteAsync(Workload(5), default);

            Assert.Equal(5, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Equal(199, _usage.UsedNow(other, ProductModule.Audit));
        }

        [Fact]
        public async Task Audit_and_accounting_allowances_are_metered_separately() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _entitlements.With(ProductModule.Accounting, PlanCode.Free);

            await Service().CompleteAsync(Workload(5), default);
            await Service().CompleteAsync(AiWorkload.For(ProductModule.Accounting,
                "accounting.test", AiTiers.Basic, "system", "prompt", cost: 3), default);

            Assert.Equal(5, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Equal(3, _usage.UsedNow(Organization, ProductModule.Accounting));
        }
    }
}
