using FluentValidation;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.AI.Application.Analysis {
    public abstract class PeriodAnalysisHandlerBase {
        protected readonly IAiCompletionService Ai;
        protected readonly IEntityGuard Guard;
        protected readonly IFiscalPeriodRepository Periods;
        protected readonly ILedgerLineRepository LedgerLines;
        protected readonly IAccountRepository Accounts;
        protected readonly IActivityRecorder Activity;

        protected PeriodAnalysisHandlerBase(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity) {
            Ai = ai;
            Guard = guard;
            Periods = periods;
            LedgerLines = ledgerLines;
            Accounts = accounts;
            Activity = activity;
        }

        protected async Task<FiscalPeriod?> FindPeriodAsync(Guid entityId, Guid periodId,
            CancellationToken ct) {
            var period = await Periods.FindAsync(periodId, ct);
            return period is null || period.EntityId != entityId ? null : period;
        }

        protected async Task<Result<AiProposalResult>> RunAsync(Guid entityId,
            AccountingAiCapability capability, string instruction, string userPrompt,
            List<AiDocument> context, string activityAction, string subjectType,
            Guid subjectId, string activitySummary, CancellationToken ct) {
            var completion = await Ai.CompleteAsync(AccountingAiPrompts.Workload(capability,
                instruction, userPrompt, context), ct);

            await Activity.RecordAsync(new ActivityEntry("Accounting", activityAction,
                subjectType, subjectId, activitySummary, entityId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(capability, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class DetectAnomaliesCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class DetectAnomaliesCommandValidator : AbstractValidator<DetectAnomaliesCommand> {
        public DetectAnomaliesCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class DetectAnomaliesCommandHandler : PeriodAnalysisHandlerBase,
        IRequestHandler<DetectAnomaliesCommand, Result<AiProposalResult>> {
        private readonly IJournalEntryRepository _entries;

        public DetectAnomaliesCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity,
            IJournalEntryRepository entries)
            : base(ai, guard, periods, ledgerLines, accounts, activity) {
            _entries = entries;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(DetectAnomaliesCommand request,
            CancellationToken ct) {
            await Guard.RequireAsync(request.EntityId, ct);

            var period = await FindPeriodAsync(request.EntityId, request.PeriodId, ct);

            if (period is null) {
                return Result<AiProposalResult>.Error("The fiscal period was not found.");
            }

            var accounts = (await Accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await LedgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);
            var trialBalance = LedgerAiContext.TrialBalance(period, lines, accounts);

            if (trialBalance is null) {
                return Result<AiProposalResult>.Error(
                    "The period has no posted activity to analyze yet.");
            }

            var context = new List<AiDocument> { trialBalance };

            var periodEntries = await _entries.ListPageAsync(request.EntityId,
                JournalEntryStatus.Posted, period.StartDate, period.EndDate,
                page: 1, pageSize: 100, ct);

            if (LedgerAiContext.JournalEntries(periodEntries.Rows) is { } entries) {
                context.Add(entries with { Title = "Posted entries in the period" });
            }

            return await RunAsync(request.EntityId,
                AccountingAiCapabilities.AnomalyDetection,
                "Examine the trial balance and the period's entries for anomalies an " +
                "accountant should investigate: balances against the account's natural " +
                "side, accounts inconsistent with their type, unusual amounts or " +
                "round-number patterns, duplicate-looking or reversing activity, and " +
                "entries whose memo does not match their accounts. Rank by significance " +
                "and say why each matters.",
                $"Detect anomalies in '{period.Name}'.", context,
                "ai.anomaly_detection", "FiscalPeriod", period.Id,
                $"AI anomaly detection was run over '{period.Name}'.", ct);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class AnalyzeFinancialsCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class AnalyzeFinancialsCommandValidator
        : AbstractValidator<AnalyzeFinancialsCommand> {
        public AnalyzeFinancialsCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class AnalyzeFinancialsCommandHandler : PeriodAnalysisHandlerBase,
        IRequestHandler<AnalyzeFinancialsCommand, Result<AiProposalResult>> {
        private readonly IEntityRepository _entities;

        public AnalyzeFinancialsCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity,
            IEntityRepository entities)
            : base(ai, guard, periods, ledgerLines, accounts, activity) {
            _entities = entities;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            AnalyzeFinancialsCommand request, CancellationToken ct) {
            var entity = await Guard.RequireAsync(request.EntityId, ct);

            var period = await FindPeriodAsync(request.EntityId, request.PeriodId, ct);

            if (period is null) {
                return Result<AiProposalResult>.Error("The fiscal period was not found.");
            }

            var accounts = (await Accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await LedgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);
            var statements = LedgerAiContext.PeriodStatements(period, lines, accounts);

            if (statements is null) {
                return Result<AiProposalResult>.Error(
                    "The period has no posted activity to analyze yet.");
            }

            var context = new List<AiDocument> {
                LedgerAiContext.EntityOverview(entity,
                    await Periods.ListAsync(entity.Id, ct)),
                statements
            };

            if (LedgerAiContext.TrialBalance(period, lines, accounts) is { } trialBalance) {
                context.Add(trialBalance);
            }

            return await RunAsync(request.EntityId,
                AccountingAiCapabilities.FinancialAnalysis,
                "Perform a financial analysis of this entity as of the period end: " +
                "profitability, liquidity, leverage and the composition of the balance " +
                "sheet, computing the ratios the data supports and stating the formula " +
                "used. Assess strengths, weaknesses and trends the figures support, and " +
                "clearly separate what the data shows from what would need more context.",
                $"Analyze the entity's financials as of '{period.Name}'.", context,
                "ai.financial_analysis", "FiscalPeriod", period.Id,
                $"An AI financial analysis as of '{period.Name}' was generated.", ct);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class AssistCloseCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class AssistCloseCommandValidator : AbstractValidator<AssistCloseCommand> {
        public AssistCloseCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class AssistCloseCommandHandler : PeriodAnalysisHandlerBase,
        IRequestHandler<AssistCloseCommand, Result<AiProposalResult>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IReconciliationRepository _reconciliations;

        public AssistCloseCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity,
            IJournalEntryRepository entries, IReconciliationRepository reconciliations)
            : base(ai, guard, periods, ledgerLines, accounts, activity) {
            _entries = entries;
            _reconciliations = reconciliations;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(AssistCloseCommand request,
            CancellationToken ct) {
            await Guard.RequireAsync(request.EntityId, ct);

            var period = await FindPeriodAsync(request.EntityId, request.PeriodId, ct);

            if (period is null) {
                return Result<AiProposalResult>.Error("The fiscal period was not found.");
            }

            var accounts = (await Accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await LedgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);

            var context = new List<AiDocument>();

            if (LedgerAiContext.TrialBalance(period, lines, accounts) is { } trialBalance) {
                context.Add(trialBalance);
            }

            var drafts = await _entries.ListPageAsync(request.EntityId,
                JournalEntryStatus.Draft, period.StartDate, period.EndDate,
                page: 1, pageSize: 100, ct);

            context.Add(new AiDocument("Draft entries dated in the period",
                drafts.Rows.Count == 0
                    ? "None — no drafts block closing this period."
                    : string.Join('\n', drafts.Rows.Select(entry =>
                        $"- #{entry.EntryNumber} {entry.EntryDate:yyyy-MM-dd} " +
                        $"{entry.Memo} — {entry.TotalDebits:N2}"))));

            var reconciliations = await _reconciliations.ListAsync(request.EntityId, null, ct);

            context.Add(new AiDocument("Reconciliations",
                reconciliations.Count == 0
                    ? "No reconciliations have been performed for this entity."
                    : string.Join('\n', reconciliations.Select(reconciliation => {
                        var account = accounts.GetValueOrDefault(reconciliation.AccountId);
                        return $"- [{reconciliation.Status}] {account?.Code ?? "?"} " +
                            $"{account?.Name ?? "Unknown"} as of " +
                            $"{reconciliation.StatementDate:yyyy-MM-dd}" +
                            (reconciliation.Difference is { } difference
                                ? $" (difference {difference:N2})"
                                : "");
                    }))));

            return await RunAsync(request.EntityId, AccountingAiCapabilities.CloseAssistance,
                "Act as a controller preparing to close this fiscal period: list what " +
                "blocks the close (draft entries in the period), what should be finished " +
                "or verified first (unreconciled or differing accounts, unusual balances, " +
                "missing accruals the data hints at), and produce a prioritized close " +
                "checklist. Be explicit about what looks ready.",
                $"Prepare the close review for '{period.Name}'.", context,
                "ai.close_assistance", "FiscalPeriod", period.Id,
                $"An AI close review for '{period.Name}' was generated.", ct);
        }
    }
}
