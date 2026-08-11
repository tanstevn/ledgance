using FluentValidation;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.AI.Application.Suggestions {
    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class SuggestJournalEntryCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }

        /// <summary>
        /// A plain-language description of the business transaction to record, e.g.
        /// "paid 5,000 office rent for March by bank transfer".
        /// </summary>
        public string TransactionDescription { get; set; } = string.Empty;
    }

    public class SuggestJournalEntryCommandValidator
        : AbstractValidator<SuggestJournalEntryCommand> {
        public SuggestJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.TransactionDescription).NotEmpty().MaximumLength(2000);
        }
    }

    public class SuggestJournalEntryCommandHandler
        : IRequestHandler<SuggestJournalEntryCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IAccountRepository _accounts;
        private readonly IJournalEntryRepository _entries;
        private readonly IActivityRecorder _activity;

        public SuggestJournalEntryCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IAccountRepository accounts, IJournalEntryRepository entries,
            IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _accounts = accounts;
            _entries = entries;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            SuggestJournalEntryCommand request, CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var accounts = await _accounts.ListAsync(request.EntityId, ct);
            var chart = LedgerAiContext.ChartOfAccounts(accounts);

            if (chart is null) {
                return Result<AiProposalResult>.Error(
                    "Create the entity's chart of accounts before requesting entry suggestions.");
            }

            var context = new List<AiDocument> { chart };

            var recent = await _entries.ListPageAsync(request.EntityId, null, null, null,
                page: 1, pageSize: 20, ct);

            if (LedgerAiContext.JournalEntries(recent.Rows) is { } entries) {
                context.Add(entries with { Title = "Recent journal entries" });
            }

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.EntrySuggestion,
                "Suggest a balanced double-entry journal entry for the described " +
                "transaction, using only postable accounts from the chart of accounts " +
                "(never summary or inactive accounts). Give the entry as a line table " +
                "(account code, account name, debit, credit) with a memo, explain why each " +
                "account was chosen, and list any assumptions. If the chart has no suitable " +
                "account, say which account should be created instead of forcing a fit.",
                request.TransactionDescription, context), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.entry_suggestion", "Entity", request.EntityId,
                "generated an AI journal-entry suggestion.", request.EntityId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AccountingAiCapabilities.EntrySuggestion, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class AssistReconciliationCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid ReconciliationId { get; set; }
    }

    public class AssistReconciliationCommandValidator
        : AbstractValidator<AssistReconciliationCommand> {
        public AssistReconciliationCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.ReconciliationId).NotEmpty();
        }
    }

    public class AssistReconciliationCommandHandler
        : IRequestHandler<AssistReconciliationCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IReconciliationRepository _reconciliations;
        private readonly IAccountRepository _accounts;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IActivityRecorder _activity;

        public AssistReconciliationCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IReconciliationRepository reconciliations, IAccountRepository accounts,
            ILedgerLineRepository ledgerLines, IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _reconciliations = reconciliations;
            _accounts = accounts;
            _ledgerLines = ledgerLines;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            AssistReconciliationCommand request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var reconciliation = await _reconciliations.FindAsync(request.ReconciliationId, ct);

            if (reconciliation is null || reconciliation.EntityId != request.EntityId) {
                return Result<AiProposalResult>.Error("The reconciliation was not found.");
            }

            var account = await _accounts.FindAsync(reconciliation.AccountId, ct);

            if (account is null) {
                return Result<AiProposalResult>.Error("The reconciled account was not found.");
            }

            var lines = await _ledgerLines.ListByAccountAsync(request.EntityId,
                reconciliation.AccountId, null, reconciliation.StatementDate, ct);

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.ReconciliationAssistance,
                "Help resolve this reconciliation: explain the remaining difference, " +
                "identify uncleared lines that plausibly match the statement (and why), " +
                "suggest what the difference could consist of (timing items, missing " +
                "entries, errors), and list the next concrete steps. Never invent statement " +
                "transactions you were not shown.",
                "Help me resolve this reconciliation.",
                [LedgerAiContext.Reconciliation(reconciliation, account, lines)]), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.reconciliation_assistance", "Reconciliation", reconciliation.Id,
                $"generated AI assistance for the reconciliation of account {account.Code}.",
                request.EntityId), ct);

            return Result<AiProposalResult>.Success(AiProposalResult.From(
                AccountingAiCapabilities.ReconciliationAssistance, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class ExplainStatementsCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class ExplainStatementsCommandValidator
        : AbstractValidator<ExplainStatementsCommand> {
        public ExplainStatementsCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class ExplainStatementsCommandHandler
        : IRequestHandler<ExplainStatementsCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IFiscalPeriodRepository _periods;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IActivityRecorder _activity;

        public ExplainStatementsCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _periods = periods;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            ExplainStatementsCommand request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<AiProposalResult>.Error("The fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);
            var statements = LedgerAiContext.PeriodStatements(period, lines, accounts);

            if (statements is null) {
                return Result<AiProposalResult>.Error(
                    "The period has no posted activity to explain yet.");
            }

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.StatementExplanation,
                "Explain these financial statements to a business owner without an " +
                "accounting background: what the income statement and balance sheet say, " +
                "what drives the result, how liquidity and financing look, and which " +
                "figures deserve a closer look. Distinguish observation from interpretation.",
                $"Explain the financial statements for '{period.Name}'.", [statements]), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.statement_explanation", "FiscalPeriod", period.Id,
                $"generated an AI statement explanation for {period.Name}.",
                request.EntityId), ct);

            return Result<AiProposalResult>.Success(AiProposalResult.From(
                AccountingAiCapabilities.StatementExplanation, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class AnalyzeVarianceCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid BasePeriodId { get; set; }
        public Guid ComparePeriodId { get; set; }
    }

    public class AnalyzeVarianceCommandValidator : AbstractValidator<AnalyzeVarianceCommand> {
        public AnalyzeVarianceCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.BasePeriodId).NotEmpty();
            RuleFor(x => x.ComparePeriodId).NotEmpty()
                .NotEqual(x => x.BasePeriodId)
                .WithMessage("Choose two different fiscal periods to compare.");
        }
    }

    public class AnalyzeVarianceCommandHandler
        : IRequestHandler<AnalyzeVarianceCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IFiscalPeriodRepository _periods;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IActivityRecorder _activity;

        public AnalyzeVarianceCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _periods = periods;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AnalyzeVarianceCommand request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var basePeriod = await _periods.FindAsync(request.BasePeriodId, ct);
            var comparePeriod = await _periods.FindAsync(request.ComparePeriodId, ct);

            if (basePeriod is null || basePeriod.EntityId != request.EntityId
                || comparePeriod is null || comparePeriod.EntityId != request.EntityId) {
                return Result<AiProposalResult>.Error("A fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);

            var context = new List<AiDocument>();

            foreach (var period in new[] { basePeriod, comparePeriod }) {
                var lines = await _ledgerLines.ListForEntityAsync(request.EntityId, null,
                    period.EndDate, ct);

                if (LedgerAiContext.PeriodStatements(period, lines, accounts)
                    is { } statements) {
                    context.Add(statements);
                }
            }

            if (context.Count < 2) {
                return Result<AiProposalResult>.Error(
                    "Both periods need posted activity before a variance analysis.");
            }

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.VarianceAnalysis,
                "Compare the two periods' statements: quantify the significant variances " +
                "in revenue, expenses and position, explain the most plausible drivers " +
                "visible in the data, and flag variances that warrant investigation. " +
                "Present the comparison as a table followed by commentary.",
                $"Analyze the variance between '{basePeriod.Name}' and '{comparePeriod.Name}'.",
                context), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.variance_analysis", "Entity", request.EntityId,
                $"generated an AI variance analysis of {basePeriod.Name} against {comparePeriod.Name}.",
                request.EntityId), ct);

            return Result<AiProposalResult>.Success(AiProposalResult.From(
                AccountingAiCapabilities.VarianceAnalysis, completion));
        }
    }
}
