using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Application.Reporting;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
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
    public class AuditAgenticReportTests {
        private const string SectionJson = """
            {"sections":[{"section":"ExecutiveSummary","heading":"Executive summary",
              "content":"[NOT IN THE ENGAGEMENT RECORD: no procedures were recorded]",
              "sources":[]}]}
            """;

        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryGeneratedReportRepository _reports = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly FakeAgentToolClient _openClaw = new(AiProviders.OpenClaw);

        private static CurrentUser Manager() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute,
                    AuditEngagementPermissions.Manage]);

        private DomainEngagement Seed(Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

            _engagements.Engagements.Add(engagement);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id, teamUserId.Value,
                    EngagementRole.Manager));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user, PlanCode plan) {
            _openClaw.Turns.Enqueue(new AgentTurn(SectionJson, null));

            var harness = new MediatorTestHarness(user)
                .WithHandler<RunAgenticReportWorkflowCommand, Result<AgenticReportResult>,
                    RunAgenticReportWorkflowCommandHandler>()
                .WithService<IGeneratedReportRepository>(_reports)
                .WithService<IActivityRecorder>(_activity);

            harness.Entitlements.With(ProductModule.Audit, plan);

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            harness.WithService<IAgentRunner>(new AgentRunnerService(harness.CurrentUser,
                harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_openClaw], [], NullLogger<AgentRunnerService>.Instance));

            return harness;
        }

        [Fact]
        public async Task Every_plan_below_medium_growth_is_refused_agentic_reporting() {
            var user = Manager();
            var engagement = Seed(user.UserId);

            foreach (var plan in new[] {
                PlanCode.Free, PlanCode.AuditMicro, PlanCode.AuditMicroGrowth,
                PlanCode.AuditSmall, PlanCode.AuditMedium
            }) {
                await Assert.ThrowsAsync<EntitlementException>(() =>
                    Harness(user, plan).SendAsync(new RunAgenticReportWorkflowCommand {
                        EngagementId = engagement.Id
                    }));
            }

            Assert.Empty(_openClaw.Calls);
            Assert.Empty(_reports.Reports);
        }

        [Fact]
        public async Task Medium_growth_runs_the_workflow_and_stores_a_draft_for_review() {
            var user = Manager();
            var engagement = Seed(user.UserId);

            var result = await Harness(user, PlanCode.AuditMediumGrowth)
                .SendAsync(new RunAgenticReportWorkflowCommand {
                    EngagementId = engagement.Id
                });

            Assert.True(result.Successful);
            Assert.Equal(nameof(GeneratedReportStatus.Draft), result.Data!.Report.Status);
            Assert.Equal(AuditAiCapabilities.AgenticReport.Key, result.Data.Report.Capability);

            var stored = Assert.Single(_reports.Reports);
            Assert.True(stored.IsAwaitingReview);
            Assert.Contains(_activity.Entries, entry => entry.Action == "ai.agentic_report");
        }

        [Fact]
        public async Task A_non_team_member_cannot_run_the_agentic_workflow() {
            var engagement = Seed();

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Harness(Manager(), PlanCode.AuditMediumGrowth)
                    .SendAsync(new RunAgenticReportWorkflowCommand {
                        EngagementId = engagement.Id
                    }));

            Assert.Empty(_openClaw.Calls);
        }

        /// <summary>
        /// The agent's tools are bound to the engagement it was started on. Nothing it can call
        /// takes an engagement id, so it cannot reach a second engagement even if it tries.
        /// </summary>
        [Fact]
        public async Task The_agents_tools_are_confined_to_the_one_engagement() {
            var user = Manager();
            var engagement = Seed(user.UserId);
            Seed(user.UserId);

            await Harness(user, PlanCode.AuditMediumGrowth)
                .SendAsync(new RunAgenticReportWorkflowCommand {
                    EngagementId = engagement.Id
                });

            var tools = Assert.Single(_openClaw.Calls).Tools;

            Assert.NotEmpty(tools);
            Assert.All(tools, tool =>
                Assert.DoesNotContain("engagementId", tool.ParametersSchema));
        }

        [Fact]
        public async Task The_agent_is_instructed_not_to_fill_gaps_in_the_record() {
            var user = Manager();
            var engagement = Seed(user.UserId);

            var result = await Harness(user, PlanCode.AuditMediumGrowth)
                .SendAsync(new RunAgenticReportWorkflowCommand {
                    EngagementId = engagement.Id
                });

            var section = Assert.Single(result.Data!.Report.Sections);
            Assert.Contains("NOT IN THE ENGAGEMENT RECORD", section.Content);
        }
    }
}
