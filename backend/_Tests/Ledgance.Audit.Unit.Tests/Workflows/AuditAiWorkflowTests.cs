using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Analysis;
using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Application.Drafting;
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
using Ledgance.TestInfrastructure;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    public class AuditAiWorkflowTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryRiskRepository _risks = new();
        private readonly InMemoryTrialBalanceRepository _trialBalances = new();
        private readonly StubClientLookup _clients = new();
        private readonly FakeAiCompletionService _ai = new();
        private readonly RecordingActivityRecorder _activity = new();

        private static CurrentUser Member() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute]);

        private DomainEngagement SeedEngagement(Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());
            _engagements.Engagements.Add(engagement);
            _clients.ActiveClients.Add(engagement.ClientId);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id,
                    teamUserId.Value, EngagementRole.Senior));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<SuggestRisksCommand, Result<AiProposalResult>,
                    SuggestRisksCommandHandler>()
                .WithHandler<AskAuditAssistantCommand, Result<AiProposalResult>,
                    AskAuditAssistantCommandHandler>()
                .WithHandler<DetectAnomaliesCommand, Result<AiProposalResult>,
                    DetectAnomaliesCommandHandler>()
                .WithHandler<GetAuditAiCapabilitiesQuery,
                    Result<IEnumerable<AuditAiCapabilityRow>>,
                    GetAuditAiCapabilitiesQueryHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IRiskRepository>(_risks)
                .WithService<ITrialBalanceRepository>(_trialBalances)
                .WithService<IClientLookup>(_clients)
                .WithService<IAiCompletionService>(_ai)
                .WithService<IActivityRecorder>(_activity);

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            return harness;
        }

        [Fact]
        public async Task A_non_team_member_cannot_run_engagement_ai() {
            var engagement = SeedEngagement();
            var harness = Harness(Member());

            await Assert.ThrowsAsync<ForbiddenException>(() => harness.SendAsync(
                new SuggestRisksCommand { EngagementId = engagement.Id }));

            Assert.Empty(_ai.Workloads);
        }

        [Fact]
        public async Task A_team_member_receives_a_tier_tagged_proposal() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            var harness = Harness(user);

            var result = await harness.SendAsync(
                new SuggestRisksCommand { EngagementId = engagement.Id });

            Assert.True(result.Successful);
            Assert.Equal(AuditAiCapabilities.RiskSuggestions.Key, result.Data!.Capability);
            Assert.False(string.IsNullOrEmpty(result.Data.Disclaimer));

            var workload = Assert.Single(_ai.Workloads);
            Assert.Equal(ProductModule.Audit, workload.Module);
            Assert.Equal(AiTiers.Advanced, workload.RequiredTier);
            Assert.Contains(workload.Context,
                document => document.Title == "Engagement overview");

            Assert.Contains(_activity.Entries,
                entry => entry.Action == "ai.risk_suggestions");
        }

        [Fact]
        public async Task The_assistant_works_without_an_engagement_and_sends_no_context() {
            var harness = Harness(Member());

            var result = await harness.SendAsync(new AskAuditAssistantCommand {
                Question = "What is performance materiality?"
            });

            Assert.True(result.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Equal(AiTiers.Basic, workload.RequiredTier);
            Assert.Empty(workload.Context);
            Assert.Empty(_activity.Entries);
        }

        [Fact]
        public async Task Anomaly_detection_requires_an_imported_trial_balance() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            var harness = Harness(user);

            var withoutTrialBalance = await harness.SendAsync(
                new DetectAnomaliesCommand { EngagementId = engagement.Id });

            Assert.False(withoutTrialBalance.Successful);
            Assert.Empty(_ai.Workloads);

            _trialBalances.Imports.Add(TrialBalanceImport.Create(engagement.Id,
                TrialBalanceSource.ExternalCsv, "FY2026",
                [new TrialBalanceLine("1000", "Cash", 500, 0),
                 new TrialBalanceLine("3000", "Equity", 0, 500)],
                user.UserId));

            var withTrialBalance = await harness.SendAsync(
                new DetectAnomaliesCommand { EngagementId = engagement.Id });

            Assert.True(withTrialBalance.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Contains(workload.Context,
                document => document.Title.StartsWith("Trial balance"));
        }

        [Fact]
        public async Task The_capability_catalog_reflects_the_plans_ai_tier() {
            var harness = Harness(Member());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.Free);

            var result = await harness.SendAsync(new GetAuditAiCapabilitiesQuery());
            var rows = result.Data!.ToDictionary(row => row.Key);

            Assert.True(rows[AuditAiCapabilities.Assistant.Key].Included);
            Assert.True(rows[AuditAiCapabilities.DocumentSummary.Key].Included);
            Assert.False(rows[AuditAiCapabilities.RiskSuggestions.Key].Included);
            Assert.False(rows[AuditAiCapabilities.ReportDraft.Key].Included);

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditOrganization);
            var upgraded = (await harness.SendAsync(new GetAuditAiCapabilitiesQuery()))
                .Data!.ToDictionary(row => row.Key);

            Assert.True(upgraded[AuditAiCapabilities.RiskSuggestions.Key].Included);
            Assert.True(upgraded[AuditAiCapabilities.ReportDraft.Key].Included);
        }
    }
}
