using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Portfolio;
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
    /// Cross-engagement AI is where an authorization mistake would be widest, so these tests
    /// assert what the model is actually shown, not only whether the call succeeds.
    /// </summary>
    public class AuditPortfolioAiTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryRiskRepository _risks = new();
        private readonly InMemoryFindingRepository _findings = new();
        private readonly StubClientLookup _clients = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly FakeAiChatClient _anthropic =
            new(AiProviders.Anthropic, _ => "portfolio answer");

        private static CurrentUser User(OrganizationRole role) =>
            TestIdentity.User(role,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Manage]);

        private DomainEngagement Seed(string name, Guid? clientId = null,
            Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(clientId ?? Guid.NewGuid(), name,
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

            _engagements.Engagements.Add(engagement);
            _clients.ActiveClients.Add(engagement.ClientId);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id, teamUserId.Value,
                    EngagementRole.Senior));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user, PlanCode plan) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<AnalyzePortfolioCommand, Result<AiProposalResult>,
                    AnalyzePortfolioCommandHandler>()
                .WithHandler<GeneratePortfolioReportCommand, Result<AiProposalResult>,
                    GeneratePortfolioReportCommandHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IRiskRepository>(_risks)
                .WithService<IFindingRepository>(_findings)
                .WithService<IClientLookup>(_clients)
                .WithService<IActivityRecorder>(_activity);

            harness.Entitlements.With(ProductModule.Audit, plan);

            harness.WithService<IAiCompletionService>(new AiCompletionService(
                harness.CurrentUser, harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_anthropic], NullLogger<AiCompletionService>.Instance));

            return harness;
        }

        [Fact]
        public async Task Plans_below_medium_cannot_reason_across_engagements() {
            var user = User(OrganizationRole.Member);
            Seed("FY2026", teamUserId: user.UserId);

            foreach (var plan in new[] {
                PlanCode.Free, PlanCode.AuditMicro, PlanCode.AuditMicroGrowth,
                PlanCode.AuditSmall
            }) {
                await Assert.ThrowsAsync<EntitlementException>(() =>
                    Harness(user, plan).SendAsync(new AnalyzePortfolioCommand()));
            }

            Assert.Empty(_anthropic.Calls);
        }

        [Fact]
        public async Task Medium_analyses_only_the_engagements_the_caller_is_assigned_to() {
            var user = User(OrganizationRole.Member);
            Seed("Mine FY2026", teamUserId: user.UserId);
            Seed("Someone else's FY2026");

            var result = await Harness(user, PlanCode.AuditMedium)
                .SendAsync(new AnalyzePortfolioCommand());

            Assert.True(result.Successful);

            var prompt = Assert.Single(_anthropic.Calls).UserPrompt;
            Assert.Contains("Mine FY2026", prompt);
            Assert.DoesNotContain("Someone else's FY2026", prompt);
        }

        [Fact]
        public async Task An_administrator_sees_the_whole_organizations_portfolio() {
            var admin = User(OrganizationRole.Admin);
            Seed("Alpha FY2026");
            Seed("Beta FY2026");

            var result = await Harness(admin, PlanCode.AuditMedium)
                .SendAsync(new AnalyzePortfolioCommand());

            Assert.True(result.Successful);

            var prompt = Assert.Single(_anthropic.Calls).UserPrompt;
            Assert.Contains("Alpha FY2026", prompt);
            Assert.Contains("Beta FY2026", prompt);
        }

        [Fact]
        public async Task A_caller_assigned_to_nothing_gets_no_portfolio_at_all() {
            var user = User(OrganizationRole.Member);
            Seed("Not mine");

            var result = await Harness(user, PlanCode.AuditMedium)
                .SendAsync(new AnalyzePortfolioCommand());

            Assert.False(result.Successful);
            Assert.Empty(_anthropic.Calls);
        }

        [Fact]
        public async Task A_client_scoped_report_covers_only_that_clients_engagements() {
            var user = User(OrganizationRole.Admin);
            var client = Guid.NewGuid();
            Seed("Client FY2025", client);
            Seed("Client FY2026", client);
            Seed("Another client FY2026");

            var result = await Harness(user, PlanCode.AuditMedium)
                .SendAsync(new GeneratePortfolioReportCommand { ClientId = client });

            Assert.True(result.Successful);

            var prompt = Assert.Single(_anthropic.Calls).UserPrompt;
            Assert.Contains("Client FY2025", prompt);
            Assert.Contains("Client FY2026", prompt);
            Assert.DoesNotContain("Another client FY2026", prompt);
        }

        [Fact]
        public async Task A_portfolio_report_is_held_to_the_reporting_discipline() {
            var user = User(OrganizationRole.Admin);
            Seed("Alpha FY2026");

            await Harness(user, PlanCode.AuditMedium)
                .SendAsync(new GeneratePortfolioReportCommand());

            var systemPrompt = Assert.Single(_anthropic.Calls).SystemPrompt;
            Assert.Contains("Never invent evidence", systemPrompt);
        }
    }
}
