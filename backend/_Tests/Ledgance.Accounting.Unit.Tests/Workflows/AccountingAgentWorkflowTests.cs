using Ledgance.Accounting.AI.Application.Agent;
using Ledgance.Accounting.AI.Application.Assistant;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class AccountingAgentWorkflowTests {
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly FakeAgentToolClient _openClaw = new(AiProviders.OpenClaw);

        private static CurrentUser Member() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute]);

        private LedgerHarness Harness(CurrentUser user) {
            var harness = new LedgerHarness(user);

            harness.Harness
                .WithHandler<RunAccountingAgentCommand, Result<AgentRunReport>,
                    RunAccountingAgentCommandHandler>()
                .WithHandler<GetAccountingAiCapabilitiesQuery,
                    Result<IEnumerable<AccountingAiCapabilityRow>>,
                    GetAccountingAiCapabilitiesQueryHandler>()
                .WithService<IAgentRunner>(new AgentRunnerService(harness.Harness.CurrentUser,
                    harness.Entitlements, _usage, _periods, _costs,
                    new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                    [_openClaw], [], NullLogger<AgentRunnerService>.Instance));

            return harness;
        }

        private static (AccountingEntity Entity, FiscalPeriod Period) SeedBooks(
            LedgerHarness harness) {
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            harness.Entities.Entities.Add(entity);

            var period = FiscalPeriod.Open(entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            harness.Periods.Periods.Add(period);

            var cash = Account.Open(entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            var sales = Account.Open(entity.Id, "4010", "Sales", AccountType.Revenue, "",
                null);
            harness.Accounts.Accounts.AddRange([cash, sales]);

            var entry = JournalEntry.Draft(entity.Id, 1, new DateOnly(2026, 3, 10),
                "Cash sale", "", [
                    JournalLine.Create(cash.Id, "", 500, 0),
                    JournalLine.Create(sales.Id, "", 0, 500)
                ], Guid.NewGuid());
            entry.Post(period, Guid.NewGuid());
            harness.Entries.Entries.Add(entry);
            harness.LedgerLines.AddRangeAsync(entry.ToLedgerLines(), entity.Id,
                CancellationToken.None).Wait();

            return (entity, period);
        }

        private static RunAccountingAgentCommand Command(Guid entityId) =>
            new() {
                EntityId = entityId,
                Goal = "Verify that March's trial balance is consistent."
            };

        [Fact]
        public async Task A_user_without_the_read_permission_cannot_run_the_agent() {
            var harness = Harness(TestIdentity.User(OrganizationRole.Viewer));
            var (entity, _) = SeedBooks(harness);
            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingEnterprise);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(Command(entity.Id)));

            Assert.Empty(_openClaw.Calls);
        }

        [Fact]
        public async Task A_plan_below_agentic_is_refused() {
            var harness = Harness(Member());
            var (entity, _) = SeedBooks(harness);
            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingProfessional);

            await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(Command(entity.Id)));

            Assert.Empty(_openClaw.Calls);
        }

        [Fact]
        public async Task An_unknown_entity_is_rejected_before_the_agent_starts() {
            var harness = Harness(Member());
            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingEnterprise);

            await Assert.ThrowsAsync<DomainRuleException>(
                () => harness.SendAsync(Command(Guid.NewGuid())));

            Assert.Empty(_openClaw.Calls);
        }

        [Fact]
        public async Task The_agent_reads_the_books_through_the_real_pipeline() {
            var harness = Harness(Member());
            var (entity, period) = SeedBooks(harness);
            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingEnterprise);

            _openClaw.Turns.Enqueue(new AgentTurn(null, new AgentToolCall(
                "get_trial_balance", $$"""{"periodId":"{{period.Id}}"}""")));
            _openClaw.Turns.Enqueue(new AgentTurn("March balances.", null));

            var result = await harness.SendAsync(Command(entity.Id));

            Assert.True(result.Successful);
            Assert.Equal(AccountingAiCapabilities.Agent.Key, result.Data!.Capability);
            Assert.Equal("March balances.", result.Data.Answer);

            var step = Assert.Single(result.Data.Steps);
            Assert.Equal("get_trial_balance", step.Tool);
            Assert.Contains("1010", step.Result);

            Assert.Contains(harness.Activity.Entries,
                entry => entry.Action == "ai.agent");
            Assert.Equal(1, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Accounting));
        }

        [Fact]
        public async Task The_capability_catalog_includes_the_agent_only_at_agentic_plans() {
            var harness = Harness(Member());

            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingProfessional);
            var reasoning = (await harness.SendAsync(new GetAccountingAiCapabilitiesQuery()))
                .Data!.Single(row => row.Key == AccountingAiCapabilities.Agent.Key);
            Assert.False(reasoning.Included);

            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingEnterprise);
            var agentic = (await harness.SendAsync(new GetAccountingAiCapabilitiesQuery()))
                .Data!.Single(row => row.Key == AccountingAiCapabilities.Agent.Key);
            Assert.True(agentic.Included);
        }
    }
}
