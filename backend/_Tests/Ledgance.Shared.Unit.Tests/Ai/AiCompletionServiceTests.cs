using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ledgance.Shared.Unit.Tests.Ai {
    public class AiCompletionServiceTests {
        private readonly FakeCurrentUserAccessor _user = new(TestIdentity.User());
        private readonly FakeEntitlementService _entitlements = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly FakeAiChatClient _ollama = new(AiProviders.Ollama);
        private readonly FakeAiChatClient _openAi = new(AiProviders.OpenAI);
        private readonly FakeAiChatClient _anthropic = new(AiProviders.Anthropic);

        private AiCompletionService Service() =>
            new(_user, _entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_ollama, _openAi, _anthropic],
                NullLogger<AiCompletionService>.Instance);

        private static AiWorkload Workload(string tier,
            IReadOnlyList<AiDocument>? context = null) =>
            AiWorkload.For(ProductModule.Audit, "audit.test", tier,
                "You are a test assistant.", "Answer the question.", context);

        [Fact]
        public async Task A_basic_workload_routes_to_the_basic_provider() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            var completion = await Service().CompleteAsync(Workload(AiTiers.Basic), default);

            Assert.Equal(AiProviders.Ollama, completion.Provider);
            Assert.Single(_ollama.Calls);
            Assert.Empty(_openAi.Calls);
            Assert.Empty(_anthropic.Calls);
        }

        [Fact]
        public async Task A_workload_above_the_plans_tier_is_refused_not_escalated() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => Service().CompleteAsync(Workload(AiTiers.Reasoning), default));

            Assert.Contains(AiTiers.Reasoning, exception.Message);
            Assert.Empty(_anthropic.Calls);
            Assert.Equal(0, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));
        }

        [Fact]
        public async Task A_reasoning_plan_routes_reasoning_work_to_the_reasoning_provider() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditSmall);

            var completion = await Service()
                .CompleteAsync(Workload(AiTiers.Reasoning), default);

            Assert.Equal(AiProviders.Anthropic, completion.Provider);
            Assert.Equal("claude-opus-5", completion.Model);
        }

        [Fact]
        public async Task The_monthly_unit_limit_blocks_further_requests() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _usage.Seed(TestIdentity.DefaultOrganizationId, ProductModule.Audit, 200);

            var exception = await Assert.ThrowsAsync<AiUsageLimitException>(
                () => Service().CompleteAsync(Workload(AiTiers.Basic), default));

            Assert.Contains("AI credits", exception.Message);
            Assert.Empty(_ollama.Calls);
        }

        [Fact]
        public async Task Usage_is_recorded_only_on_successful_completions() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            await Service().CompleteAsync(Workload(AiTiers.Basic), default);
            Assert.Equal(1, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));

            _ollama.Throws = new InvalidOperationException("provider down");

            await Assert.ThrowsAsync<AiUnavailableException>(
                () => Service().CompleteAsync(Workload(AiTiers.Basic), default));

            Assert.Equal(1, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));
        }

        [Fact]
        public async Task A_failed_provider_falls_back_down_the_tier_chain_never_up() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditSmall);
            _anthropic.Throws = new HttpRequestException("anthropic down");

            var completion = await Service()
                .CompleteAsync(Workload(AiTiers.Reasoning), default);

            Assert.Equal(AiProviders.OpenAI, completion.Provider);
            Assert.Single(_anthropic.Calls);
            Assert.Single(_openAi.Calls);
        }

        [Fact]
        public async Task When_every_provider_fails_the_service_reports_unavailable() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);
            _ollama.Throws = new HttpRequestException("down");

            await Assert.ThrowsAsync<AiUnavailableException>(
                () => Service().CompleteAsync(Workload(AiTiers.Basic), default));
        }

        [Fact]
        public async Task Oversized_context_documents_are_truncated_to_the_plan_limit() {
            _entitlements.With(ProductModule.Audit, PlanCode.Free);

            var hugeDocument = new AiDocument("Ledger", new string('x', 400_000));

            await Service().CompleteAsync(Workload(AiTiers.Basic, [hugeDocument]), default);

            var sentPrompt = Assert.Single(_ollama.Calls).UserPrompt;
            Assert.Contains("[truncated]", sentPrompt);
            Assert.True(AiUsage.EstimateTokens(sentPrompt) <= 16_000,
                $"Prompt estimated at {AiUsage.EstimateTokens(sentPrompt)} tokens " +
                "exceeds the Free plan's 16k context limit.");
        }

        [Fact]
        public async Task An_unauthenticated_caller_cannot_reach_a_provider() {
            var service = new AiCompletionService(new FakeCurrentUserAccessor(),
                _entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_ollama], NullLogger<AiCompletionService>.Instance);

            await Assert.ThrowsAsync<UnauthenticatedException>(
                () => service.CompleteAsync(Workload(AiTiers.Basic), default));

            Assert.Empty(_ollama.Calls);
        }

        /// <summary>
        /// Report completeness is gated independently of reasoning depth: Micro buys the
        /// 'advanced' tier but only section-level report writing, so a whole-report workload at
        /// the same tier must still be refused.
        /// </summary>
        [Fact]
        public async Task A_report_workload_beyond_the_plans_report_scope_is_refused() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMicro);

            var wholeReport = AiWorkload.For(ProductModule.Audit, "audit.report_draft",
                AiTiers.Advanced, "system", "draft the report", null,
                AiReportScopes.FullDraft);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => Service().CompleteAsync(wholeReport, default));

            Assert.Contains(AiReportScopes.FullDraft, exception.Message);
            Assert.Empty(_openAi.Calls);
            Assert.Equal(0, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));
        }

        [Fact]
        public async Task The_same_report_workload_runs_once_the_plan_grants_the_scope() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMicroGrowth);

            var completion = await Service().CompleteAsync(AiWorkload.For(ProductModule.Audit,
                "audit.report_draft", AiTiers.Advanced, "system", "draft the report", null,
                AiReportScopes.FullDraft), default);

            Assert.Equal(AiProviders.OpenAI, completion.Provider);
        }

        [Fact]
        public async Task A_workload_beyond_the_plans_analysis_scope_is_refused() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMicroGrowth);

            var acrossEngagements = AiWorkload.For(ProductModule.Audit,
                "audit.portfolio_intelligence", AiTiers.Advanced, "system", "compare", null,
                AiReportScopes.None, AiAnalysisScopes.Portfolio);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => Service().CompleteAsync(acrossEngagements, default));

            Assert.Contains(AiAnalysisScopes.Portfolio, exception.Message);
        }

        /// <summary>
        /// An entitlement value outside the ladder — a typo in configuration, or a tampered
        /// per-organization override — must deny rather than rank above everything.
        /// </summary>
        [Fact]
        public async Task An_unrecognised_report_scope_grant_denies_rather_than_escalates() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth,
                new Dictionary<string, string> {
                    [Entitlements.AiReportScope] = "everything"
                });

            await Assert.ThrowsAsync<EntitlementException>(
                () => Service().CompleteAsync(AiWorkload.For(ProductModule.Audit,
                    "audit.report_draft", AiTiers.Basic, "system", "draft", null,
                    AiReportScopes.Sections), default));
        }

        [Fact]
        public void Configuration_overrides_the_default_route_for_a_tier() {
            var settings = new AiSettings {
                Routing = {
                    [AiTiers.Basic] = new AiSettings.AiRouteSettings {
                        Provider = AiProviders.OpenAI,
                        Model = "gpt-4o-mini",
                        MaxOutputTokens = 1024
                    }
                }
            };

            var route = new ConfiguredAiModelRouter(Options.Create(settings))
                .Resolve(AiTiers.Basic);

            Assert.Equal(AiProviders.OpenAI, route.Provider);
            Assert.Equal("gpt-4o-mini", route.Model);
        }
    }
}
