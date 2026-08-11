using FluentValidation;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.ChartOfAccounts;
using Ledgance.Accounting.Ledger.Application.Entities;
using Ledgance.Accounting.Ledger.Application.Journal;
using Ledgance.Accounting.Ledger.Application.Ledger;
using Ledgance.Accounting.Ledger.Application.Periods;
using Ledgance.Accounting.Ledger.Application.Reconciliations;
using Ledgance.Accounting.Ledger.Application.Reports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using System.Text.Json;

namespace Ledgance.Accounting.AI.Application.Agent {
    public class AgentStepView {
        public string Tool { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    public class AgentRunReport {
        public string Capability { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<AgentStepView> Steps { get; set; } = [];
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TurnsUsed { get; set; }

        public string Disclaimer { get; set; } =
            "AI-generated analysis produced by an agent over authorized, read-only data. " +
            "It assists professional judgment and is not verified accounting fact.";

        public static AgentRunReport From(AccountingAiCapability capability,
            AgentRunResult run) =>
            new() {
                Capability = capability.Key,
                Answer = run.Answer,
                Steps = run.Steps.Select(step => new AgentStepView {
                    Tool = step.Tool,
                    Arguments = step.ArgumentsJson,
                    Result = step.Result
                }).ToList(),
                Provider = run.Provider,
                Model = run.Model,
                TurnsUsed = run.TurnsUsed
            };
    }

    internal static class AgentToolJson {
        private static readonly JsonSerializerOptions Options = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static async Task<string> DispatchAsync<T>(IMediator mediator,
            IRequest<Result<T>> request, CancellationToken ct) {
            var result = await mediator.SendAsync(request, ct);

            return result.Successful
                ? JsonSerializer.Serialize(result.Data, Options)
                : "Error: " + string.Join("; ", result.Errors ?? ["The request failed."]);
        }

        public static async Task<string> DispatchPageAsync<T>(IMediator mediator,
            IRequest<PaginatedResult<T>> request, CancellationToken ct) {
            var result = await mediator.SendAsync(request, ct);
            return JsonSerializer.Serialize(result, Options);
        }

        public static Guid GetGuid(JsonElement arguments, string name) =>
            arguments.TryGetProperty(name, out var property)
                && Guid.TryParse(property.GetString(), out var value)
                ? value
                : Guid.Empty;

        public static Guid? GetOptionalGuid(JsonElement arguments, string name) {
            var value = GetGuid(arguments, name);
            return value == Guid.Empty ? null : value;
        }

        public static DateOnly? GetDate(JsonElement arguments, string name) =>
            arguments.TryGetProperty(name, out var property)
                && DateOnly.TryParse(property.GetString(), out var value)
                ? value
                : null;

        public const string NoParameters = """{"type":"object","properties":{}}""";
        public const string PeriodParameter =
            """{"type":"object","properties":{"periodId":{"type":"string","description":"A fiscal period id from list_fiscal_periods."}},"required":["periodId"]}""";
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class RunAccountingAgentCommand : ICommand<Result<AgentRunReport>> {
        public Guid EntityId { get; set; }

        /// <summary>
        /// What the agent should investigate, e.g. "find out why the March bank
        /// reconciliation does not close and what is driving the difference".
        /// </summary>
        public string Goal { get; set; } = string.Empty;
    }

    public class RunAccountingAgentCommandValidator
        : AbstractValidator<RunAccountingAgentCommand> {
        public RunAccountingAgentCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.Goal).NotEmpty().MaximumLength(4000);
        }
    }

    public class RunAccountingAgentCommandHandler
        : IRequestHandler<RunAccountingAgentCommand, Result<AgentRunReport>> {
        private const string SystemPrompt =
            "You are an investigative AI agent for accounting professionals inside the " +
            "Ledgance Accounting platform, a double-entry bookkeeping system. Work strictly " +
            "from what your tools return — they are your only source of accounting data, " +
            "and they enforce the caller's authorization and the accounting rules. When a " +
            "tool denies access or returns an error, respect it and work with what you " +
            "have. Investigate step by step, then give a structured, sourced answer in the " +
            "entity's base currency, distinguishing observation from interpretation. Your " +
            "output is a proposal the accountant must review, never verified accounting fact.";

        private readonly IAgentRunner _agent;
        private readonly IMediator _mediator;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public RunAccountingAgentCommandHandler(IAgentRunner agent, IMediator mediator,
            IEntityGuard guard, IActivityRecorder activity) {
            _agent = agent;
            _mediator = mediator;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<AgentRunReport>> HandleAsync(
            RunAccountingAgentCommand request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var run = await _agent.RunAsync(new AgentWorkload(ProductModule.Accounting,
                AccountingAiCapabilities.Agent.Key, request.Goal, SystemPrompt,
                BuildTools(request.EntityId)), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "ai.agent",
                "Entity", request.EntityId,
                $"ran an AI agent investigation over {run.Steps.Count} tool steps: {Trim(request.Goal)}",
                request.EntityId), ct);

            return Result<AgentRunReport>.Success(
                AgentRunReport.From(AccountingAiCapabilities.Agent, run));
        }

        private static string Trim(string goal) =>
            goal.Length <= 120 ? goal : goal[..120] + "…";

        /// <summary>
        /// The agent's whole world: read-only queries of this entity's books, dispatched
        /// through the mediator so each call re-runs the full pipeline as the calling user.
        /// The entity id is fixed here — the agent can never choose another entity.
        /// </summary>
        private List<AgentTool> BuildTools(Guid entityId) => [
            new AgentTool("get_entity_overview",
                "The entity's name, base currency and archive status.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetEntityQuery { EntityId = entityId }, ct)),

            new AgentTool("get_chart_of_accounts",
                "All accounts with code, name, type and active status.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetChartOfAccountsQuery {
                        EntityId = entityId,
                        IncludeInactive = true
                    }, ct)),

            new AgentTool("list_fiscal_periods",
                "The fiscal periods with dates and open/closed status.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetFiscalPeriodsQuery { EntityId = entityId }, ct)),

            new AgentTool("list_journal_entries",
                "Journal entries, newest first (50 per page). Optionally filter by status " +
                "(Draft, Posted, Reversed) and date range.",
                """{"type":"object","properties":{"status":{"type":"string","enum":["Draft","Posted","Reversed"]},"from":{"type":"string","description":"yyyy-MM-dd"},"to":{"type":"string","description":"yyyy-MM-dd"},"page":{"type":"integer"}}}""",
                (arguments, ct) => AgentToolJson.DispatchPageAsync(_mediator,
                    new GetJournalEntriesQuery {
                        EntityId = entityId,
                        Status = arguments.TryGetProperty("status", out var status)
                            && Enum.TryParse<JournalEntryStatus>(status.GetString(),
                                out var parsed)
                            ? parsed
                            : null,
                        From = AgentToolJson.GetDate(arguments, "from"),
                        To = AgentToolJson.GetDate(arguments, "to"),
                        Page = arguments.TryGetProperty("page", out var page)
                            && page.TryGetInt32(out var pageNumber) && pageNumber > 0
                            ? pageNumber
                            : 1,
                        PageSize = 50
                    }, ct)),

            new AgentTool("get_journal_entry",
                "One journal entry's full line detail.",
                """{"type":"object","properties":{"entryId":{"type":"string","description":"A journal entry id from list_journal_entries."}},"required":["entryId"]}""",
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetJournalEntryQuery {
                        EntityId = entityId,
                        EntryId = AgentToolJson.GetGuid(arguments, "entryId")
                    }, ct)),

            new AgentTool("get_general_ledger",
                "One account's ledger lines with running balance, optionally date-bounded.",
                """{"type":"object","properties":{"accountId":{"type":"string","description":"An account id from get_chart_of_accounts."},"from":{"type":"string","description":"yyyy-MM-dd"},"to":{"type":"string","description":"yyyy-MM-dd"}},"required":["accountId"]}""",
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetGeneralLedgerQuery {
                        EntityId = entityId,
                        AccountId = AgentToolJson.GetGuid(arguments, "accountId"),
                        From = AgentToolJson.GetDate(arguments, "from"),
                        To = AgentToolJson.GetDate(arguments, "to")
                    }, ct)),

            new AgentTool("get_trial_balance",
                "The trial balance as of a fiscal period's end.",
                AgentToolJson.PeriodParameter,
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetTrialBalanceQuery {
                        EntityId = entityId,
                        PeriodId = AgentToolJson.GetGuid(arguments, "periodId")
                    }, ct)),

            new AgentTool("get_income_statement",
                "The income statement for a fiscal period.",
                AgentToolJson.PeriodParameter,
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetIncomeStatementQuery {
                        EntityId = entityId,
                        PeriodId = AgentToolJson.GetGuid(arguments, "periodId")
                    }, ct)),

            new AgentTool("get_balance_sheet",
                "The balance sheet as of a fiscal period's end.",
                AgentToolJson.PeriodParameter,
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetBalanceSheetQuery {
                        EntityId = entityId,
                        PeriodId = AgentToolJson.GetGuid(arguments, "periodId")
                    }, ct)),

            new AgentTool("list_reconciliations",
                "The reconciliations with status, statement balances and differences. " +
                "Optionally filter by account.",
                """{"type":"object","properties":{"accountId":{"type":"string"}}}""",
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetReconciliationsQuery {
                        EntityId = entityId,
                        AccountId = AgentToolJson.GetOptionalGuid(arguments, "accountId")
                    }, ct)),

            new AgentTool("get_reconciliation",
                "One reconciliation's cleared and uncleared lines with the working difference.",
                """{"type":"object","properties":{"reconciliationId":{"type":"string","description":"A reconciliation id from list_reconciliations."}},"required":["reconciliationId"]}""",
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetReconciliationQuery {
                        EntityId = entityId,
                        ReconciliationId = AgentToolJson.GetGuid(arguments,
                            "reconciliationId")
                    }, ct))
        ];
    }
}
