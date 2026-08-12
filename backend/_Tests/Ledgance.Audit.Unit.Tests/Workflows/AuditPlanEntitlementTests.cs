using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Domain;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    /// <summary>
    /// The plan-to-capability matrix, asserted through the capability catalogue the API serves.
    /// This is the contract the billing page advertises and the AI services enforce; if a
    /// capability moves between plans, this test is what says so.
    /// </summary>
    public class AuditPlanEntitlementTests {
        private static async Task<IReadOnlyDictionary<string, AuditAiCapabilityRow>>
            CapabilitiesFor(PlanCode plan) {
            var harness = new MediatorTestHarness(TestIdentity.User(OrganizationRole.Member))
                .WithHandler<GetAuditAiCapabilitiesQuery,
                    Result<IEnumerable<AuditAiCapabilityRow>>,
                    GetAuditAiCapabilitiesQueryHandler>();

            harness.Entitlements.With(ProductModule.Audit, plan);

            var result = await harness.SendAsync(new GetAuditAiCapabilitiesQuery());

            return result.Data!.ToDictionary(row => row.Key);
        }

        private static async Task AssertIncluded(PlanCode plan,
            IEnumerable<AuditAiCapability> included,
            IEnumerable<AuditAiCapability> excluded) {
            var rows = await CapabilitiesFor(plan);

            foreach (var capability in included) {
                Assert.True(rows[capability.Key].Included,
                    $"{plan} should include {capability.Key}.");
            }

            foreach (var capability in excluded) {
                Assert.False(rows[capability.Key].Included,
                    $"{plan} must not include {capability.Key}.");
            }
        }

        [Fact]
        public Task Free_gets_assistance_and_summaries_but_no_report_generation() =>
            AssertIncluded(PlanCode.Free,
                included: [
                    AuditAiCapabilities.Assistant,
                    AuditAiCapabilities.DocumentSummary,
                    AuditAiCapabilities.FindingSummary,
                    AuditAiCapabilities.EngagementSummary,
                    AuditAiCapabilities.NoteDraft,
                    AuditAiCapabilities.WordingAssistance
                ],
                excluded: [
                    AuditAiCapabilities.RiskSuggestions,
                    AuditAiCapabilities.WorkingPaperDraft,
                    AuditAiCapabilities.ReportSection,
                    AuditAiCapabilities.ReportDraft,
                    AuditAiCapabilities.EngagementReport,
                    AuditAiCapabilities.PortfolioReport,
                    AuditAiCapabilities.AgenticReport
                ]);

        [Fact]
        public Task Micro_adds_planning_and_drafting_but_only_report_sections() =>
            AssertIncluded(PlanCode.AuditMicro,
                included: [
                    AuditAiCapabilities.Assistant,
                    AuditAiCapabilities.PlanAssistance,
                    AuditAiCapabilities.MaterialityAssistance,
                    AuditAiCapabilities.RiskSuggestions,
                    AuditAiCapabilities.ProcedureSuggestions,
                    AuditAiCapabilities.WorkingPaperDraft,
                    AuditAiCapabilities.FindingDraft,
                    AuditAiCapabilities.ReportSection
                ],
                excluded: [
                    AuditAiCapabilities.EngagementIntelligence,
                    AuditAiCapabilities.EvidenceAnalysis,
                    AuditAiCapabilities.ReportDraft,
                    AuditAiCapabilities.EngagementReport,
                    AuditAiCapabilities.RiskAnalysis,
                    AuditAiCapabilities.AgenticReport
                ]);

        [Fact]
        public Task Micro_growth_adds_engagement_intelligence_and_the_complete_draft_report() =>
            AssertIncluded(PlanCode.AuditMicroGrowth,
                included: [
                    AuditAiCapabilities.ReportSection,
                    AuditAiCapabilities.EngagementIntelligence,
                    AuditAiCapabilities.EvidenceAnalysis,
                    AuditAiCapabilities.ReportDraft,
                    AuditAiCapabilities.ReportConsistency
                ],
                excluded: [
                    AuditAiCapabilities.RiskAnalysis,
                    AuditAiCapabilities.AnomalyDetection,
                    AuditAiCapabilities.ReviewAssistance,
                    AuditAiCapabilities.EngagementReport,
                    AuditAiCapabilities.PortfolioIntelligence,
                    AuditAiCapabilities.Agent
                ]);

        [Fact]
        public Task Small_adds_workflows_review_assistance_and_the_full_engagement_report() =>
            AssertIncluded(PlanCode.AuditSmall,
                included: [
                    AuditAiCapabilities.ReportDraft,
                    AuditAiCapabilities.RiskAnalysis,
                    AuditAiCapabilities.AnomalyDetection,
                    AuditAiCapabilities.ReviewAssistance,
                    AuditAiCapabilities.EngagementReport
                ],
                excluded: [
                    AuditAiCapabilities.PortfolioIntelligence,
                    AuditAiCapabilities.PortfolioReport,
                    AuditAiCapabilities.Agent,
                    AuditAiCapabilities.AgenticReport
                ]);

        [Fact]
        public Task Medium_adds_multi_engagement_and_client_intelligence() =>
            AssertIncluded(PlanCode.AuditMedium,
                included: [
                    AuditAiCapabilities.EngagementReport,
                    AuditAiCapabilities.PortfolioIntelligence,
                    AuditAiCapabilities.PortfolioReport
                ],
                excluded: [
                    AuditAiCapabilities.Agent,
                    AuditAiCapabilities.AgenticReport
                ]);

        [Fact]
        public async Task Medium_growth_adds_agentic_workflows_and_agentic_reporting() {
            await AssertIncluded(PlanCode.AuditMediumGrowth,
                included: [
                    AuditAiCapabilities.PortfolioReport,
                    AuditAiCapabilities.Agent,
                    AuditAiCapabilities.AgenticReport
                ],
                excluded: []);

            var rows = await CapabilitiesFor(PlanCode.AuditMediumGrowth);
            Assert.All(rows.Values, row => Assert.True(row.Included));
        }

        [Fact]
        public async Task Enterprise_includes_every_capability() {
            var rows = await CapabilitiesFor(PlanCode.AuditEnterprise);

            Assert.Equal(AuditAiCapabilities.All.Count, rows.Count);
            Assert.All(rows.Values, row => Assert.True(row.Included, row.Key));
        }

        /// <summary>
        /// A capability with no home plan can never be sold; a plan-gating change that orphans
        /// one should fail here rather than quietly ship a dead endpoint.
        /// </summary>
        [Fact]
        public async Task Every_capability_is_reachable_on_some_plan() {
            var free = await CapabilitiesFor(PlanCode.Free);
            var top = await CapabilitiesFor(PlanCode.AuditEnterprise);

            Assert.All(AuditAiCapabilities.All, capability =>
                Assert.True(free[capability.Key].Included || top[capability.Key].Included,
                    $"{capability.Key} is not included on any plan."));
        }

        /// <summary>
        /// The catalogue names the upgrade, so a locked capability in the UI can say which plan
        /// unlocks it without the client holding plan rules of its own.
        /// </summary>
        [Theory]
        [InlineData("audit.assistant", PlanCode.Free)]
        [InlineData("audit.finding_summary", PlanCode.Free)]
        [InlineData("audit.report_section", PlanCode.AuditMicro)]
        [InlineData("audit.report_draft", PlanCode.AuditMicroGrowth)]
        [InlineData("audit.evidence_analysis", PlanCode.AuditMicroGrowth)]
        [InlineData("audit.engagement_report", PlanCode.AuditSmall)]
        [InlineData("audit.review_assistance", PlanCode.AuditSmall)]
        [InlineData("audit.portfolio_report", PlanCode.AuditMedium)]
        [InlineData("audit.agentic_report", PlanCode.AuditMediumGrowth)]
        public async Task A_capability_names_the_cheapest_plan_that_includes_it(string key,
            PlanCode expected) {
            var rows = await CapabilitiesFor(PlanCode.Free);

            Assert.Equal(expected.ToString(), rows[key].RequiredPlan);
        }

        [Fact]
        public async Task Turning_ai_off_removes_every_capability_regardless_of_plan() {
            var harness = new MediatorTestHarness(TestIdentity.User(OrganizationRole.Member))
                .WithHandler<GetAuditAiCapabilitiesQuery,
                    Result<IEnumerable<AuditAiCapabilityRow>>,
                    GetAuditAiCapabilitiesQueryHandler>();

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditEnterprise,
                new Dictionary<string, string> { [Entitlements.AiEnabled] = "false" });

            var result = await harness.SendAsync(new GetAuditAiCapabilitiesQuery());

            Assert.All(result.Data!, row => Assert.False(row.Included));
        }
    }
}
