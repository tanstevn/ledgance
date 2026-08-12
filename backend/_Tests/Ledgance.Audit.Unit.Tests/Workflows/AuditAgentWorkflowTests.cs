using Ledgance.Audit.AI.Application.Agent;
using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.AccountingContext;
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
using AuditTrialBalanceQuery =
    Ledgance.Audit.Engagement.Application.AccountingContext.GetTrialBalanceQuery;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    public class AuditAgentWorkflowTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryTrialBalanceRepository _trialBalances = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly FakeAgentToolClient _openClaw = new(AiProviders.OpenClaw);

        private static CurrentUser Member() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute]);

        private DomainEngagement SeedEngagement(Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());
            _engagements.Engagements.Add(engagement);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id,
                    teamUserId.Value, EngagementRole.Senior));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<RunAuditAgentCommand, Result<AgentRunReport>,
                    RunAuditAgentCommandHandler>()
                .WithHandler<AuditTrialBalanceQuery, Result<TrialBalanceView>,
                    GetTrialBalanceQueryHandler>()
                .WithHandler<GetAuditAiCapabilitiesQuery,
                    Result<IEnumerable<AuditAiCapabilityRow>>,
                    GetAuditAiCapabilitiesQueryHandler>()
                .WithService<ITrialBalanceRepository>(_trialBalances)
                .WithService<IActivityRecorder>(_activity);

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            harness.WithService<IAgentRunner>(new AgentRunnerService(harness.CurrentUser,
                harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_openClaw], [], NullLogger<AgentRunnerService>.Instance));

            return harness;
        }

        private static RunAuditAgentCommand Command(Guid engagementId) =>
            new() {
                EngagementId = engagementId,
                Goal = "Check whether the trial balance is consistent with the risks."
            };

        [Fact]
        public async Task A_non_team_member_cannot_run_the_agent() {
            var engagement = SeedEngagement();
            var harness = Harness(Member());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(Command(engagement.Id)));

            Assert.Empty(_openClaw.Calls);
        }

        [Fact]
        public async Task A_plan_below_agentic_is_refused() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            var harness = Harness(user);
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditSmall);

            await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(Command(engagement.Id)));

            Assert.Empty(_openClaw.Calls);
        }

        [Fact]
        public async Task The_agent_reads_the_trial_balance_through_the_real_pipeline() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            _trialBalances.Imports.Add(TrialBalanceImport.Create(engagement.Id,
                TrialBalanceSource.ExternalCsv, "FY2026",
                [new TrialBalanceLine("1000", "Cash", 500, 0),
                 new TrialBalanceLine("3000", "Equity", 0, 500)],
                user.UserId));

            var harness = Harness(user);
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth);

            _openClaw.Turns.Enqueue(new AgentTurn(null,
                new AgentToolCall("get_trial_balance", "{}")));
            _openClaw.Turns.Enqueue(new AgentTurn("The books balance.", null));

            var result = await harness.SendAsync(Command(engagement.Id));

            Assert.True(result.Successful);
            Assert.Equal(AuditAiCapabilities.Agent.Key, result.Data!.Capability);
            Assert.Equal("The books balance.", result.Data.Answer);
            Assert.False(string.IsNullOrEmpty(result.Data.Disclaimer));

            var step = Assert.Single(result.Data.Steps);
            Assert.Equal("get_trial_balance", step.Tool);
            Assert.Contains("1000", step.Result);

            Assert.Contains(_activity.Entries, entry => entry.Action == "ai.agent");

            // One reservation for the whole run, at the capability's cost — not one per turn.
            Assert.Equal(AuditAiCapabilities.Agent.Cost,
                _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));
        }

        [Fact]
        public async Task A_failing_tool_is_contained_in_the_transcript() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            var harness = Harness(user);
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth);

            _openClaw.Turns.Enqueue(new AgentTurn(null,
                new AgentToolCall("get_trial_balance", "{}")));
            _openClaw.Turns.Enqueue(new AgentTurn("No trial balance exists yet.", null));

            var result = await harness.SendAsync(Command(engagement.Id));

            Assert.True(result.Successful);
            var step = Assert.Single(result.Data!.Steps);
            Assert.StartsWith("Error:", step.Result);
        }

        [Fact]
        public async Task The_capability_catalog_includes_the_agent_only_at_agentic_plans() {
            var harness = Harness(Member());

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditSmall);
            var reasoning = (await harness.SendAsync(new GetAuditAiCapabilitiesQuery()))
                .Data!.Single(row => row.Key == AuditAiCapabilities.Agent.Key);
            Assert.False(reasoning.Included);

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMediumGrowth);
            var agentic = (await harness.SendAsync(new GetAuditAiCapabilitiesQuery()))
                .Data!.Single(row => row.Key == AuditAiCapabilities.Agent.Key);
            Assert.True(agentic.Included);
        }
    }
}
