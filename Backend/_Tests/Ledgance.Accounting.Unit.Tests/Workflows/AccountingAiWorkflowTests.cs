using Ledgance.Accounting.AI.Application;
using Ledgance.Accounting.AI.Application.Analysis;
using Ledgance.Accounting.AI.Application.Assistant;
using Ledgance.Accounting.AI.Application.Suggestions;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class AccountingAiWorkflowTests {
        private readonly InMemoryEntityRepository _entities = new();
        private readonly InMemoryAccountRepository _accounts = new();
        private readonly InMemoryFiscalPeriodRepository _periods = new();
        private readonly InMemoryJournalEntryRepository _entries = new();
        private readonly InMemoryLedgerLineRepository _ledgerLines = new();
        private readonly InMemoryReconciliationRepository _reconciliations = new();
        private readonly FakeAiCompletionService _ai = new();
        private readonly RecordingActivityRecorder _activity = new();

        private static CurrentUser Member() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute]);

        private static CurrentUser Manager() =>
            TestIdentity.User(OrganizationRole.Manager,
                permissions: [AccountingLedgerPermissions.Read,
                    AccountingLedgerPermissions.Contribute,
                    AccountingLedgerPermissions.Manage]);

        private MediatorTestHarness Harness(CurrentUser user) =>
            new MediatorTestHarness(user)
                .WithHandler<AskAccountingAssistantCommand, Result<AiProposalResult>,
                    AskAccountingAssistantCommandHandler>()
                .WithHandler<ExplainJournalEntryCommand, Result<AiProposalResult>,
                    ExplainJournalEntryCommandHandler>()
                .WithHandler<SummarizePeriodCommand, Result<AiProposalResult>,
                    SummarizePeriodCommandHandler>()
                .WithHandler<SuggestJournalEntryCommand, Result<AiProposalResult>,
                    SuggestJournalEntryCommandHandler>()
                .WithHandler<AssistReconciliationCommand, Result<AiProposalResult>,
                    AssistReconciliationCommandHandler>()
                .WithHandler<DetectAnomaliesCommand, Result<AiProposalResult>,
                    DetectAnomaliesCommandHandler>()
                .WithHandler<AssistCloseCommand, Result<AiProposalResult>,
                    AssistCloseCommandHandler>()
                .WithHandler<GetAccountingAiCapabilitiesQuery,
                    Result<IEnumerable<AccountingAiCapabilityRow>>,
                    GetAccountingAiCapabilitiesQueryHandler>()
                .WithService<IEntityRepository>(_entities)
                .WithService<IAccountRepository>(_accounts)
                .WithService<IFiscalPeriodRepository>(_periods)
                .WithService<IJournalEntryRepository>(_entries)
                .WithService<ILedgerLineRepository>(_ledgerLines)
                .WithService<IReconciliationRepository>(_reconciliations)
                .WithService<IEntityGuard>(new EntityGuard(_entities))
                .WithService<IAiCompletionService>(_ai)
                .WithService<IActivityRecorder>(_activity);

        private AccountingEntity SeedEntity() {
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            _entities.Entities.Add(entity);
            return entity;
        }

        private (FiscalPeriod Period, Account Cash, Account Sales) SeedBooks(
            AccountingEntity entity) {
            var period = FiscalPeriod.Open(entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            _periods.Periods.Add(period);

            var cash = Account.Open(entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            var sales = Account.Open(entity.Id, "4010", "Sales", AccountType.Revenue, "",
                null);
            _accounts.Accounts.AddRange([cash, sales]);

            var entry = JournalEntry.Draft(entity.Id, 1, new DateOnly(2026, 3, 10),
                "Cash sale", "", [
                    JournalLine.Create(cash.Id, "", 500, 0),
                    JournalLine.Create(sales.Id, "", 0, 500)
                ], Guid.NewGuid());
            entry.Post(period, Guid.NewGuid());
            _entries.Entries.Add(entry);
            _ledgerLines.AddRangeAsync(entry.ToLedgerLines(), entity.Id,
                CancellationToken.None).Wait();

            return (period, cash, sales);
        }

        [Fact]
        public async Task A_user_without_the_read_permission_cannot_use_accounting_ai() {
            var harness = Harness(TestIdentity.User(OrganizationRole.Viewer));

            await Assert.ThrowsAsync<ForbiddenException>(() => harness.SendAsync(
                new AskAccountingAssistantCommand { Question = "What is a debit?" }));

            Assert.Empty(_ai.Workloads);
        }

        [Fact]
        public async Task Ai_disabled_by_the_plan_is_rejected_before_any_provider_call() {
            var harness = Harness(Member());
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free,
                new Dictionary<string, string> { [Entitlements.AiEnabled] = "false" });

            await Assert.ThrowsAsync<EntitlementException>(() => harness.SendAsync(
                new AskAccountingAssistantCommand { Question = "What is a debit?" }));

            Assert.Empty(_ai.Workloads);
        }

        [Fact]
        public async Task The_assistant_works_without_an_entity_and_sends_no_context() {
            var harness = Harness(Member());

            var result = await harness.SendAsync(new AskAccountingAssistantCommand {
                Question = "When should I accrue an expense?"
            });

            Assert.True(result.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Equal(ProductModule.Accounting, workload.Module);
            Assert.Equal(AiTiers.Basic, workload.RequiredTier);
            Assert.Empty(workload.Context);
            Assert.Empty(_activity.Entries);
        }

        [Fact]
        public async Task The_assistant_with_an_entity_sends_its_books_as_context() {
            var entity = SeedEntity();
            SeedBooks(entity);
            var harness = Harness(Member());

            var result = await harness.SendAsync(new AskAccountingAssistantCommand {
                Question = "How is my cash position?",
                EntityId = entity.Id
            });

            Assert.True(result.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Contains(workload.Context,
                document => document.Title == "Entity overview");
            Assert.Contains(workload.Context,
                document => document.Title == "Chart of accounts");
            Assert.Contains(_activity.Entries, entry => entry.Action == "ai.assistant");
        }

        [Fact]
        public async Task Explaining_an_entry_of_another_entity_is_rejected_without_an_ai_call() {
            var entity = SeedEntity();
            var other = AccountingEntity.Create("Other Books", "", "PHP");
            _entities.Entities.Add(other);
            SeedBooks(other);
            var foreignEntry = _entries.Entries.Single();

            var harness = Harness(Member());

            var result = await harness.SendAsync(new ExplainJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = foreignEntry.Id
            });

            Assert.False(result.Successful);
            Assert.Empty(_ai.Workloads);
        }

        [Fact]
        public async Task Explaining_an_entry_sends_its_lines_and_records_activity() {
            var entity = SeedEntity();
            SeedBooks(entity);
            var entry = _entries.Entries.Single();
            var harness = Harness(Member());

            var result = await harness.SendAsync(new ExplainJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = entry.Id
            });

            Assert.True(result.Successful);
            Assert.Equal(AccountingAiCapabilities.EntryExplanation.Key,
                result.Data!.Capability);
            Assert.False(string.IsNullOrEmpty(result.Data.Disclaimer));

            var workload = Assert.Single(_ai.Workloads);
            var document = Assert.Single(workload.Context);
            Assert.StartsWith("Journal entry #1", document.Title);
            Assert.Contains("1010", document.Content);

            Assert.Contains(_activity.Entries,
                entry2 => entry2.Action == "ai.entry_explanation");
        }

        [Fact]
        public async Task Entry_suggestions_require_a_chart_of_accounts() {
            var entity = SeedEntity();
            var harness = Harness(Member());

            var withoutChart = await harness.SendAsync(new SuggestJournalEntryCommand {
                EntityId = entity.Id,
                TransactionDescription = "Paid 5,000 office rent by bank transfer"
            });

            Assert.False(withoutChart.Successful);
            Assert.Empty(_ai.Workloads);

            SeedBooks(entity);

            var withChart = await harness.SendAsync(new SuggestJournalEntryCommand {
                EntityId = entity.Id,
                TransactionDescription = "Paid 5,000 office rent by bank transfer"
            });

            Assert.True(withChart.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Equal(AiTiers.Advanced, workload.RequiredTier);
            Assert.Contains(workload.Context,
                document => document.Title == "Chart of accounts");
        }

        [Fact]
        public async Task Anomaly_detection_needs_posted_activity_and_runs_at_reasoning_tier() {
            var entity = SeedEntity();
            var emptyPeriod = FiscalPeriod.Open(entity.Id, "April 2026",
                new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            _periods.Periods.Add(emptyPeriod);
            var harness = Harness(Member());

            var withoutActivity = await harness.SendAsync(new DetectAnomaliesCommand {
                EntityId = entity.Id,
                PeriodId = emptyPeriod.Id
            });

            Assert.False(withoutActivity.Successful);
            Assert.Empty(_ai.Workloads);

            var (period, _, _) = SeedBooks(entity);

            var withActivity = await harness.SendAsync(new DetectAnomaliesCommand {
                EntityId = entity.Id,
                PeriodId = period.Id
            });

            Assert.True(withActivity.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Equal(AiTiers.Reasoning, workload.RequiredTier);
            Assert.Contains(workload.Context,
                document => document.Title.StartsWith("Trial balance"));
        }

        [Fact]
        public async Task Close_assistance_requires_the_manage_permission() {
            var entity = SeedEntity();
            var (period, _, _) = SeedBooks(entity);

            await Assert.ThrowsAsync<ForbiddenException>(() => Harness(Member())
                .SendAsync(new AssistCloseCommand {
                    EntityId = entity.Id,
                    PeriodId = period.Id
                }));

            var result = await Harness(Manager()).SendAsync(new AssistCloseCommand {
                EntityId = entity.Id,
                PeriodId = period.Id
            });

            Assert.True(result.Successful);
            var workload = Assert.Single(_ai.Workloads);
            Assert.Contains(workload.Context,
                document => document.Title == "Draft entries dated in the period");
            Assert.Contains(workload.Context, document => document.Title == "Reconciliations");
        }

        [Fact]
        public async Task Reconciliation_assistance_sends_the_cleared_state() {
            var entity = SeedEntity();
            var (_, cash, _) = SeedBooks(entity);
            var reconciliation = Reconciliation.Start(entity.Id, cash.Id,
                new DateOnly(2026, 3, 31), 500m, Guid.NewGuid());
            _reconciliations.Reconciliations.Add(reconciliation);
            var harness = Harness(Member());

            var result = await harness.SendAsync(new AssistReconciliationCommand {
                EntityId = entity.Id,
                ReconciliationId = reconciliation.Id
            });

            Assert.True(result.Successful);
            var workload = Assert.Single(_ai.Workloads);
            var document = Assert.Single(workload.Context);
            Assert.StartsWith("Reconciliation of 1010", document.Title);
            Assert.Contains("UNCLEARED", document.Content);
            Assert.Contains(_activity.Entries,
                entry => entry.Action == "ai.reconciliation_assistance");
        }

        [Fact]
        public async Task The_capability_catalog_reflects_the_plans_ai_tier() {
            var harness = Harness(Member());
            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free);

            var free = (await harness.SendAsync(new GetAccountingAiCapabilitiesQuery()))
                .Data!.ToDictionary(row => row.Key);

            Assert.True(free[AccountingAiCapabilities.Assistant.Key].Included);
            Assert.True(free[AccountingAiCapabilities.FinancialSummary.Key].Included);
            Assert.False(free[AccountingAiCapabilities.EntrySuggestion.Key].Included);
            Assert.False(free[AccountingAiCapabilities.AnomalyDetection.Key].Included);

            harness.Entitlements.With(ProductModule.Accounting, PlanCode.AccountingSolo);
            var solo = (await harness.SendAsync(new GetAccountingAiCapabilitiesQuery()))
                .Data!.ToDictionary(row => row.Key);

            Assert.True(solo[AccountingAiCapabilities.EntrySuggestion.Key].Included);
            Assert.False(solo[AccountingAiCapabilities.AnomalyDetection.Key].Included);

            harness.Entitlements.With(ProductModule.Accounting,
                PlanCode.AccountingProfessional);
            var professional = (await harness.SendAsync(
                new GetAccountingAiCapabilitiesQuery())).Data!.ToDictionary(row => row.Key);

            Assert.True(professional[AccountingAiCapabilities.AnomalyDetection.Key].Included);
            Assert.True(professional[AccountingAiCapabilities.CloseAssistance.Key].Included);
        }
    }
}
