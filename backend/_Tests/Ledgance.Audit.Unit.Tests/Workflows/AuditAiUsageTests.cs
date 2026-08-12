using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Application.Reporting;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    /// <summary>
    /// What Audit AI actually charges, driven through the real completion service so the credits
    /// counted are the ones production would count. The catalogue is the single source of the
    /// prices, so these tests assert against it rather than restating numbers.
    /// </summary>
    public class AuditAiUsageTests {
        private static readonly Guid Organization = TestIdentity.DefaultOrganizationId;

        private const string SectionJson =
            """{"sections":[{"section":"ExecutiveSummary","heading":"Summary","content":"Text.","sources":[]}]}""";

        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryRiskRepository _risks = new();
        private readonly InMemoryProcedureRepository _procedures = new();
        private readonly InMemoryWorkingPaperRepository _papers = new();
        private readonly InMemoryEvidenceRepository _evidence = new();
        private readonly InMemoryFindingRepository _findings = new();
        private readonly InMemoryTrialBalanceRepository _trialBalances = new();
        private readonly StubClientLookup _clients = new();
        private readonly InMemoryGeneratedReportRepository _reports = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly FakeAiChatClient _ollama = new(AiProviders.Ollama, _ => SectionJson);
        private readonly FakeAiChatClient _openAi = new(AiProviders.OpenAI, _ => SectionJson);
        private readonly FakeAiChatClient _anthropic =
            new(AiProviders.Anthropic, _ => SectionJson);

        private static CurrentUser Auditor() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute,
                    AuditEngagementPermissions.Manage]);

        private DomainEngagement Seed(Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

            _engagements.Engagements.Add(engagement);
            _clients.ActiveClients.Add(engagement.ClientId);

            _findings.Findings.Add(Finding.Raise(engagement.Id, "Cut-off error",
                "Revenue recognised in the wrong period.", FindingSeverity.High,
                "Adjust the entry.", [], Guid.NewGuid()));

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id, teamUserId.Value,
                    EngagementRole.Manager));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user, PlanCode plan) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<SummarizeFindingsCommand, Result<AiProposalResult>,
                    SummarizeFindingsCommandHandler>()
                .WithHandler<GenerateReportSectionCommand, Result<AiProposalResult>,
                    GenerateReportSectionCommandHandler>()
                .WithHandler<GenerateDraftReportCommand, Result<GeneratedReportView>,
                    GenerateDraftReportCommandHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IRiskRepository>(_risks)
                .WithService<IProcedureRepository>(_procedures)
                .WithService<IWorkingPaperRepository>(_papers)
                .WithService<IEvidenceRepository>(_evidence)
                .WithService<IFindingRepository>(_findings)
                .WithService<ITrialBalanceRepository>(_trialBalances)
                .WithService<IClientLookup>(_clients)
                .WithService<IGeneratedReportRepository>(_reports)
                .WithService<IActivityRecorder>(_activity);

            harness.Entitlements.With(ProductModule.Audit, plan);

            harness.WithService(new EngagementReadSet(_engagements, _clients, _risks,
                _procedures, _papers, _evidence, _findings, _trialBalances));

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            harness.WithService<IAiCompletionService>(new AiCompletionService(
                harness.CurrentUser, harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_ollama, _openAi, _anthropic], NullLogger<AiCompletionService>.Instance));

            return harness;
        }

        [Fact]
        public async Task A_summary_costs_what_the_catalogue_says_it_costs() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            await Harness(user, PlanCode.Free).SendAsync(new SummarizeFindingsCommand {
                EngagementId = engagement.Id
            });

            Assert.Equal(AuditAiCapabilities.FindingSummary.Cost,
                _usage.UsedNow(Organization, ProductModule.Audit));
        }

        /// <summary>
        /// The point of the whole model: one API call is not one unit of cost. A whole report
        /// must be dramatically more expensive than a summary.
        /// </summary>
        [Fact]
        public async Task A_whole_report_costs_far_more_than_a_summary() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new SummarizeFindingsCommand { EngagementId = engagement.Id });

            var afterSummary = _usage.UsedNow(Organization, ProductModule.Audit);

            await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var forReport = _usage.UsedNow(Organization, ProductModule.Audit) - afterSummary;

            Assert.Equal(AuditAiCapabilities.FindingSummary.Cost, afterSummary);
            Assert.Equal(AuditAiCapabilities.ReportDraft.Cost, forReport);
            Assert.True(forReport >= afterSummary * 10,
                $"A whole report ({forReport}) should cost far more than a summary ({afterSummary}).");
        }

        [Fact]
        public async Task Usage_is_attributed_to_the_engagement_it_was_spent_on() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            await Harness(user, PlanCode.AuditMicro).SendAsync(new GenerateReportSectionCommand {
                EngagementId = engagement.Id,
                Section = AuditReportSection.Findings
            });

            var charged = Assert.Single(_usage.Reserved);

            Assert.Equal(AuditAiCapabilities.ReportSection.Key, charged.Capability);
            Assert.Equal(engagement.Id, charged.EngagementId);
            Assert.Equal(Organization, charged.OrganizationId);
            Assert.Equal(user.UserId, charged.UserId);
        }

        /// <summary>
        /// Enforcement and authorization are separate concerns and must stay that way: an
        /// operation the caller was never allowed to run cannot cost them anything.
        /// </summary>
        [Fact]
        public async Task An_unauthorized_operation_consumes_no_credits() {
            var user = Auditor();
            var engagement = Seed();

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Harness(user, PlanCode.AuditMicroGrowth)
                    .SendAsync(new GenerateDraftReportCommand {
                        EngagementId = engagement.Id
                    }));

            Assert.Equal(0, _usage.UsedNow(Organization, ProductModule.Audit));
            Assert.Empty(_usage.Reserved);
        }

        [Fact]
        public async Task A_capability_outside_the_plan_consumes_no_credits() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            await Assert.ThrowsAsync<EntitlementException>(() =>
                Harness(user, PlanCode.Free).SendAsync(new GenerateDraftReportCommand {
                    EngagementId = engagement.Id
                }));

            Assert.Equal(0, _usage.UsedNow(Organization, ProductModule.Audit));
        }

        /// <summary>
        /// The limit has to bite on the endpoint itself — a caller going straight to the API
        /// gets the same refusal the UI would have shown.
        /// </summary>
        [Fact]
        public async Task A_direct_api_call_cannot_get_past_an_exhausted_allowance() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            _usage.Seed(Organization, ProductModule.Audit, 40_000);

            var exception = await Assert.ThrowsAsync<AiUsageLimitException>(() =>
                Harness(user, PlanCode.AuditMicroGrowth)
                    .SendAsync(new GenerateDraftReportCommand {
                        EngagementId = engagement.Id
                    }));

            Assert.Contains("AI credits", exception.Message);
            Assert.Empty(_openAi.Calls);
            Assert.Empty(_reports.Reports);
        }

        /// <summary>
        /// Each plan's allowance has to carry a sensible amount of real work, not just a big
        /// number: the operations a plan is sold on must fit inside what it buys.
        /// </summary>
        [Theory]
        [InlineData(PlanCode.Free, "audit.assistant", 100)]
        [InlineData(PlanCode.AuditMicro, "audit.report_section", 500)]
        [InlineData(PlanCode.AuditMicroGrowth, "audit.report_draft", 500)]
        [InlineData(PlanCode.AuditSmall, "audit.engagement_report", 500)]
        [InlineData(PlanCode.AuditMedium, "audit.portfolio_report", 500)]
        [InlineData(PlanCode.AuditMediumGrowth, "audit.agentic_report", 500)]
        public void A_plans_allowance_carries_the_work_it_is_sold_on(PlanCode plan,
            string capability, long expectedRuns) {
            var allowance = long.Parse(
                SubscriptionPlanCatalog.For(plan)[Entitlements.AiMonthlyUnits]);
            var cost = AuditAiCapabilities.All.Single(c => c.Key == capability).Cost;

            Assert.True(allowance / cost >= expectedRuns,
                $"{plan} buys only {allowance / cost} runs of {capability}; " +
                $"at least {expectedRuns} were expected.");
        }

        /// <summary>
        /// Credits are a product measure. Nothing about which provider served a tier may change
        /// what a customer is charged.
        /// </summary>
        [Fact]
        public async Task The_provider_that_serves_a_request_does_not_change_its_price() {
            var user = Auditor();
            var engagement = Seed(user.UserId);

            await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var throughOpenAi = _usage.UsedNow(Organization, ProductModule.Audit);
            Assert.Single(_openAi.Calls);

            _openAi.Throws = new HttpRequestException("OpenAI is down.");

            await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var afterFallback = _usage.UsedNow(Organization, ProductModule.Audit);

            Assert.Single(_ollama.Calls);
            Assert.Equal(throughOpenAi * 2, afterFallback);
        }
    }
}
