using FluentValidation;
using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.AI.Application.Assistant {
    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class AskAccountingAssistantCommand : ICommand<Result<AiProposalResult>> {
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// When set, the assistant answers in the context of this entity's books. When null,
        /// it answers general accounting questions only.
        /// </summary>
        public Guid? EntityId { get; set; }
    }

    public class AskAccountingAssistantCommandValidator
        : AbstractValidator<AskAccountingAssistantCommand> {
        public AskAccountingAssistantCommandValidator() {
            RuleFor(x => x.Question).NotEmpty().MaximumLength(4000);
        }
    }

    public class AskAccountingAssistantCommandHandler
        : IRequestHandler<AskAccountingAssistantCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IAccountRepository _accounts;
        private readonly IActivityRecorder _activity;

        public AskAccountingAssistantCommandHandler(IAiCompletionService ai,
            IEntityGuard guard, IFiscalPeriodRepository periods, IAccountRepository accounts,
            IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _periods = periods;
            _accounts = accounts;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            AskAccountingAssistantCommand request, CancellationToken ct) {
            var context = new List<AiDocument>();

            if (request.EntityId is not null) {
                var entity = await _guard.RequireAsync(request.EntityId.Value, ct);

                context.Add(LedgerAiContext.EntityOverview(entity,
                    await _periods.ListAsync(entity.Id, ct)));

                if (LedgerAiContext.ChartOfAccounts(
                        await _accounts.ListAsync(entity.Id, ct)) is { } chart) {
                    context.Add(chart);
                }
            }

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.Assistant,
                "Answer the accountant's question. Use the entity context when provided; " +
                "otherwise answer from general accounting knowledge and say that no entity " +
                "context was available.",
                request.Question, context), ct);

            if (request.EntityId is not null) {
                await _activity.RecordAsync(new ActivityEntry("Accounting", "ai.assistant",
                    "Entity", request.EntityId.Value,
                    "asked the AI assistant about these books.", request.EntityId), ct);
            }

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AccountingAiCapabilities.Assistant, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class ExplainJournalEntryCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
    }

    public class ExplainJournalEntryCommandValidator
        : AbstractValidator<ExplainJournalEntryCommand> {
        public ExplainJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryId).NotEmpty();
        }
    }

    public class ExplainJournalEntryCommandHandler
        : IRequestHandler<ExplainJournalEntryCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IJournalEntryRepository _entries;
        private readonly IAccountRepository _accounts;
        private readonly IActivityRecorder _activity;

        public ExplainJournalEntryCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IJournalEntryRepository entries, IAccountRepository accounts,
            IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _entries = entries;
            _accounts = accounts;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            ExplainJournalEntryCommand request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<AiProposalResult>.Error("The journal entry was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.EntryExplanation,
                "Explain this journal entry in plain language for a non-specialist: what " +
                "business event it most likely records, how each line affects its account, " +
                "and its effect on the financial statements. Note anything unusual.",
                "Explain this journal entry.",
                [LedgerAiContext.EntryDetail(entry, accounts)]), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.entry_explanation", "JournalEntry", entry.Id,
                $"generated an AI explanation of journal entry #{entry.EntryNumber}.",
                request.EntityId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AccountingAiCapabilities.EntryExplanation, completion));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    [RequiresEntitlement(ProductModule.Accounting, Entitlements.AiEnabled)]
    public class SummarizePeriodCommand : ICommand<Result<AiProposalResult>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class SummarizePeriodCommandValidator : AbstractValidator<SummarizePeriodCommand> {
        public SummarizePeriodCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class SummarizePeriodCommandHandler
        : IRequestHandler<SummarizePeriodCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEntityGuard _guard;
        private readonly IFiscalPeriodRepository _periods;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IActivityRecorder _activity;

        public SummarizePeriodCommandHandler(IAiCompletionService ai, IEntityGuard guard,
            IFiscalPeriodRepository periods, ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IActivityRecorder activity) {
            _ai = ai;
            _guard = guard;
            _periods = periods;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(SummarizePeriodCommand request,
            CancellationToken ct) {
            var entity = await _guard.RequireAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<AiProposalResult>.Error("The fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);

            var context = new List<AiDocument> {
                LedgerAiContext.EntityOverview(entity,
                    await _periods.ListAsync(entity.Id, ct))
            };

            if (LedgerAiContext.PeriodStatements(period, lines, accounts) is { } statements) {
                context.Add(statements);
            }

            var completion = await _ai.CompleteAsync(AccountingAiPrompts.Workload(
                AccountingAiCapabilities.FinancialSummary,
                "Summarize this fiscal period's financial activity for the business owner: " +
                "what was earned and spent, the resulting position, and the few numbers most " +
                "worth attention. If there is no posted activity, say so.",
                $"Summarize the period '{period.Name}'.", context), ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "ai.financial_summary", "FiscalPeriod", period.Id,
                $"generated an AI financial summary of {period.Name}.",
                request.EntityId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AccountingAiCapabilities.FinancialSummary, completion));
        }
    }

    public class GetAccountingAiCapabilitiesQuery
        : IQuery<Result<IEnumerable<AccountingAiCapabilityRow>>> { }

    public class AccountingAiCapabilityRow {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RequiredTier { get; set; } = string.Empty;
        public bool Included { get; set; }
    }

    public class GetAccountingAiCapabilitiesQueryHandler
        : IRequestHandler<GetAccountingAiCapabilitiesQuery,
            Result<IEnumerable<AccountingAiCapabilityRow>>> {
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;

        public GetAccountingAiCapabilitiesQueryHandler(IEntitlementService entitlements,
            ICurrentUserAccessor currentUser) {
            _entitlements = entitlements;
            _currentUser = currentUser;
        }

        public async Task<Result<IEnumerable<AccountingAiCapabilityRow>>> HandleAsync(
            GetAccountingAiCapabilitiesQuery request, CancellationToken ct) {
            var entitlements = await _entitlements.GetAsync(
                _currentUser.RequireOrganizationId(), ProductModule.Accounting, ct);

            var aiEnabled = entitlements.Has(Entitlements.AiEnabled);
            var permittedTier = entitlements.Tier(Entitlements.AiMaxTier);

            return Result<IEnumerable<AccountingAiCapabilityRow>>.Success(
                AccountingAiCapabilities.All
                    .Select(capability => new AccountingAiCapabilityRow {
                        Key = capability.Key,
                        Description = capability.Description,
                        RequiredTier = capability.RequiredTier,
                        Included = aiEnabled
                            && AiTiers.Allows(permittedTier, capability.RequiredTier)
                    }));
        }
    }
}
