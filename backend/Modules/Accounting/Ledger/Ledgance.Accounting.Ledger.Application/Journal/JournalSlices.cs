using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.Ledger.Application.Journal {
    public record JournalLineInput(Guid AccountId, string Description, decimal Debit,
        decimal Credit);

    public class JournalLineInputValidator : AbstractValidator<JournalLineInput> {
        public JournalLineInputValidator() {
            RuleFor(x => x.AccountId).NotEmpty();
            RuleFor(x => x.Description).MaximumLength(300);
            RuleFor(x => x.Debit).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);
        }
    }

    internal static class JournalRules {
        public static async Task<string?> ValidateAccountsAsync(IAccountRepository accounts,
            Guid entityId, IEnumerable<Guid> accountIds, CancellationToken ct) {
            foreach (var accountId in accountIds.Distinct()) {
                var account = await accounts.FindAsync(accountId, ct);

                if (account is null || account.EntityId != entityId) {
                    return "A journal line references an account that was not found.";
                }

                if (!account.IsActive) {
                    return $"Account {account.Code} '{account.Name}' is inactive and cannot receive postings.";
                }

                if (await accounts.HasChildrenAsync(account.Id, ct)) {
                    return $"Account {account.Code} '{account.Name}' is a summary account — post to one of its sub-accounts.";
                }
            }

            return null;
        }

        public static async Task<(FiscalPeriod? Period, string? Error)> ResolveOpenPeriodAsync(
            IFiscalPeriodRepository periods, Guid entityId, DateOnly date,
            CancellationToken ct) {
            var period = await periods.FindContainingAsync(entityId, date, ct);

            if (period is null) {
                return (null, "No fiscal period covers the journal entry date.");
            }

            if (!period.IsOpen) {
                return (null, "The fiscal period covering the journal entry date is closed.");
            }

            return (period, null);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class CreateJournalEntryCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public DateOnly EntryDate { get; set; }
        public string Memo { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public List<JournalLineInput> Lines { get; set; } = [];
    }

    public class CreateJournalEntryCommandValidator
        : AbstractValidator<CreateJournalEntryCommand> {
        public CreateJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryDate).NotEmpty();
            RuleFor(x => x.Memo).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Reference).MaximumLength(100);
            RuleFor(x => x.Lines).NotEmpty();
            RuleForEach(x => x.Lines).SetValidator(new JournalLineInputValidator());
        }
    }

    public class CreateJournalEntryCommandHandler
        : IRequestHandler<CreateJournalEntryCommand, Result<Guid>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public CreateJournalEntryCommandHandler(IJournalEntryRepository entries,
            IAccountRepository accounts, IFiscalPeriodRepository periods, IEntityGuard guard,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _entries = entries;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(CreateJournalEntryCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var (period, periodError) = await JournalRules.ResolveOpenPeriodAsync(_periods,
                request.EntityId, request.EntryDate, ct);

            if (period is null) {
                return Result<Guid>.Error(periodError!);
            }

            var accountError = await JournalRules.ValidateAccountsAsync(_accounts,
                request.EntityId, request.Lines.Select(line => line.AccountId), ct);

            if (accountError is not null) {
                return Result<Guid>.Error(accountError);
            }

            var user = _currentUser.Require();
            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                ProductModule.Accounting, ct);

            var inPeriod = await _entries.CountInRangeAsync(request.EntityId,
                period.StartDate, period.EndDate, ct);
            entitlements.RequireWithinLimit(Entitlements.MaxTransactionsPerPeriod, inPeriod + 1);

            var entryNumber = await _entries.NextEntryNumberAsync(request.EntityId, ct);
            var entry = JournalEntry.Draft(request.EntityId, entryNumber, request.EntryDate,
                request.Memo, request.Reference,
                request.Lines.Select(line => JournalLine.Create(line.AccountId,
                    line.Description, line.Debit, line.Credit)),
                user.UserId);

            await _entries.AddAsync(entry, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "journal.drafted",
                "JournalEntry", entry.Id,
                $"Journal entry #{entry.EntryNumber} '{entry.Memo}' was drafted.",
                request.EntityId), ct);

            return Result<Guid>.Success(entry.Id);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class UpdateJournalEntryCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
        public DateOnly EntryDate { get; set; }
        public string Memo { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public List<JournalLineInput> Lines { get; set; } = [];
    }

    public class UpdateJournalEntryCommandValidator
        : AbstractValidator<UpdateJournalEntryCommand> {
        public UpdateJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryId).NotEmpty();
            RuleFor(x => x.EntryDate).NotEmpty();
            RuleFor(x => x.Memo).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Reference).MaximumLength(100);
            RuleFor(x => x.Lines).NotEmpty();
            RuleForEach(x => x.Lines).SetValidator(new JournalLineInputValidator());
        }
    }

    public class UpdateJournalEntryCommandHandler
        : IRequestHandler<UpdateJournalEntryCommand, Result<bool>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public UpdateJournalEntryCommandHandler(IJournalEntryRepository entries,
            IAccountRepository accounts, IFiscalPeriodRepository periods, IEntityGuard guard,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _entries = entries;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateJournalEntryCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<bool>.Error("The journal entry was not found.");
            }

            var (period, periodError) = await JournalRules.ResolveOpenPeriodAsync(_periods,
                request.EntityId, request.EntryDate, ct);

            if (period is null) {
                return Result<bool>.Error(periodError!);
            }

            var accountError = await JournalRules.ValidateAccountsAsync(_accounts,
                request.EntityId, request.Lines.Select(line => line.AccountId), ct);

            if (accountError is not null) {
                return Result<bool>.Error(accountError);
            }

            if (!period.Contains(entry.EntryDate)) {
                var entitlements = await _entitlements.GetAsync(
                    _currentUser.Require().OrganizationId, ProductModule.Accounting, ct);

                var inPeriod = await _entries.CountInRangeAsync(request.EntityId,
                    period.StartDate, period.EndDate, ct);
                entitlements.RequireWithinLimit(Entitlements.MaxTransactionsPerPeriod,
                    inPeriod + 1);
            }

            entry.UpdateDraft(request.EntryDate, request.Memo, request.Reference,
                request.Lines.Select(line => JournalLine.Create(line.AccountId,
                    line.Description, line.Debit, line.Credit)));

            await _entries.UpdateAsync(entry, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "journal.updated",
                "JournalEntry", entry.Id,
                $"Journal entry #{entry.EntryNumber} '{entry.Memo}' was updated.",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class DeleteJournalEntryCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
    }

    public class DeleteJournalEntryCommandValidator
        : AbstractValidator<DeleteJournalEntryCommand> {
        public DeleteJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryId).NotEmpty();
        }
    }

    public class DeleteJournalEntryCommandHandler
        : IRequestHandler<DeleteJournalEntryCommand, Result<bool>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public DeleteJournalEntryCommandHandler(IJournalEntryRepository entries,
            IEntityGuard guard, IActivityRecorder activity) {
            _entries = entries;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(DeleteJournalEntryCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<bool>.Error("The journal entry was not found.");
            }

            if (entry.Status != JournalEntryStatus.Draft) {
                throw new DomainRuleException(
                    "Only a draft journal entry can be deleted — posted entries are corrected by reversal.");
            }

            await _entries.DeleteAsync(entry.Id, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "journal.draft_deleted",
                "JournalEntry", entry.Id,
                $"Draft journal entry #{entry.EntryNumber} '{entry.Memo}' was deleted.",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class PostJournalEntryCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
    }

    public class PostJournalEntryCommandValidator : AbstractValidator<PostJournalEntryCommand> {
        public PostJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryId).NotEmpty();
        }
    }

    public class PostJournalEntryCommandHandler
        : IRequestHandler<PostJournalEntryCommand, Result<bool>> {
        private readonly IJournalEntryRepository _entries;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public PostJournalEntryCommandHandler(IJournalEntryRepository entries,
            ILedgerLineRepository ledgerLines, IAccountRepository accounts,
            IFiscalPeriodRepository periods, IEntityGuard guard,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _entries = entries;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(PostJournalEntryCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<bool>.Error("The journal entry was not found.");
            }

            var period = await _periods.FindContainingAsync(request.EntityId, entry.EntryDate, ct);

            if (period is null) {
                return Result<bool>.Error("No fiscal period covers the journal entry date.");
            }

            var accountError = await JournalRules.ValidateAccountsAsync(_accounts,
                request.EntityId, entry.Lines.Select(line => line.AccountId), ct);

            if (accountError is not null) {
                return Result<bool>.Error(accountError);
            }

            entry.Post(period, _currentUser.Require().UserId);

            await _entries.UpdateAsync(entry, ct);
            await _ledgerLines.AddRangeAsync(entry.ToLedgerLines(), entry.EntityId, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "journal.posted",
                "JournalEntry", entry.Id,
                $"Journal entry #{entry.EntryNumber} '{entry.Memo}' was posted ({entry.TotalDebits:0.00}).",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class ReverseJournalEntryCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
        public DateOnly ReversalDate { get; set; }
    }

    public class ReverseJournalEntryCommandValidator
        : AbstractValidator<ReverseJournalEntryCommand> {
        public ReverseJournalEntryCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.EntryId).NotEmpty();
            RuleFor(x => x.ReversalDate).NotEmpty();
        }
    }

    public class ReverseJournalEntryCommandHandler
        : IRequestHandler<ReverseJournalEntryCommand, Result<Guid>> {
        private readonly IJournalEntryRepository _entries;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ReverseJournalEntryCommandHandler(IJournalEntryRepository entries,
            ILedgerLineRepository ledgerLines, IFiscalPeriodRepository periods,
            IEntityGuard guard, IEntitlementService entitlements,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _entries = entries;
            _ledgerLines = ledgerLines;
            _periods = periods;
            _guard = guard;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(ReverseJournalEntryCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<Guid>.Error("The journal entry was not found.");
            }

            var (period, periodError) = await JournalRules.ResolveOpenPeriodAsync(_periods,
                request.EntityId, request.ReversalDate, ct);

            if (period is null) {
                return Result<Guid>.Error(periodError!);
            }

            var user = _currentUser.Require();
            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                ProductModule.Accounting, ct);

            var inPeriod = await _entries.CountInRangeAsync(request.EntityId,
                period.StartDate, period.EndDate, ct);
            entitlements.RequireWithinLimit(Entitlements.MaxTransactionsPerPeriod, inPeriod + 1);

            var reversalNumber = await _entries.NextEntryNumberAsync(request.EntityId, ct);
            var reversal = entry.Reverse(reversalNumber, request.ReversalDate, user.UserId);

            reversal.Post(period, user.UserId);
            entry.MarkReversed(reversal.Id);

            await _entries.AddAsync(reversal, ct);
            await _ledgerLines.AddRangeAsync(reversal.ToLedgerLines(), reversal.EntityId, ct);
            await _entries.UpdateAsync(entry, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "journal.reversed",
                "JournalEntry", entry.Id,
                $"Journal entry #{entry.EntryNumber} was reversed by entry #{reversal.EntryNumber}.",
                request.EntityId), ct);

            return Result<Guid>.Success(reversal.Id);
        }
    }

    public class JournalEntryRow {
        public Guid Id { get; set; }
        public long EntryNumber { get; set; }
        public DateOnly EntryDate { get; set; }
        public string Memo { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public Guid? ReversalOfEntryId { get; set; }
        public Guid? ReversedByEntryId { get; set; }
        public DateTime? PostedAt { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetJournalEntriesQuery : PaginatedRequest<JournalEntryRow> {
        public Guid EntityId { get; set; }
        public JournalEntryStatus? Status { get; set; }
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
    }

    public class GetJournalEntriesQueryHandler
        : IRequestHandler<GetJournalEntriesQuery, PaginatedResult<JournalEntryRow>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IEntityGuard _guard;

        public GetJournalEntriesQueryHandler(IJournalEntryRepository entries,
            IEntityGuard guard) {
            _entries = entries;
            _guard = guard;
        }

        public async Task<PaginatedResult<JournalEntryRow>> HandleAsync(
            GetJournalEntriesQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var result = await _entries.ListPageAsync(request.EntityId, request.Status,
                request.From, request.To, page, pageSize, ct);

            var rows = result.Rows
                .Select(entry => new JournalEntryRow {
                    Id = entry.Id,
                    EntryNumber = entry.EntryNumber,
                    EntryDate = entry.EntryDate,
                    Memo = entry.Memo,
                    Reference = entry.Reference,
                    Status = entry.Status.ToString(),
                    TotalDebits = entry.TotalDebits,
                    TotalCredits = entry.TotalCredits,
                    ReversalOfEntryId = entry.ReversalOfEntryId,
                    ReversedByEntryId = entry.ReversedByEntryId,
                    PostedAt = entry.PostedAt
                })
                .ToList();

            return new PaginatedResult<JournalEntryRow> {
                Successful = true,
                Data = rows,
                PageNumber = page,
                ItemsPerPage = pageSize,
                ResultsCount = rows.Count,
                TotalResultsCount = (int)result.TotalCount,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (decimal)pageSize)
            };
        }
    }

    public class JournalEntryLineRow {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class JournalEntryDetail : JournalEntryRow {
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? PostedBy { get; set; }
        public List<JournalEntryLineRow> Lines { get; set; } = [];
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetJournalEntryQuery : IQuery<Result<JournalEntryDetail>> {
        public Guid EntityId { get; set; }
        public Guid EntryId { get; set; }
    }

    public class GetJournalEntryQueryHandler
        : IRequestHandler<GetJournalEntryQuery, Result<JournalEntryDetail>> {
        private readonly IJournalEntryRepository _entries;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;

        public GetJournalEntryQueryHandler(IJournalEntryRepository entries,
            IAccountRepository accounts, IEntityGuard guard) {
            _entries = entries;
            _accounts = accounts;
            _guard = guard;
        }

        public async Task<Result<JournalEntryDetail>> HandleAsync(GetJournalEntryQuery request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var entry = await _entries.FindAsync(request.EntryId, ct);

            if (entry is null || entry.EntityId != request.EntityId) {
                return Result<JournalEntryDetail>.Error("The journal entry was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);

            return Result<JournalEntryDetail>.Success(new JournalEntryDetail {
                Id = entry.Id,
                EntryNumber = entry.EntryNumber,
                EntryDate = entry.EntryDate,
                Memo = entry.Memo,
                Reference = entry.Reference,
                Status = entry.Status.ToString(),
                TotalDebits = entry.TotalDebits,
                TotalCredits = entry.TotalCredits,
                ReversalOfEntryId = entry.ReversalOfEntryId,
                ReversedByEntryId = entry.ReversedByEntryId,
                CreatedBy = entry.CreatedBy,
                CreatedAt = entry.CreatedAt,
                PostedBy = entry.PostedBy,
                PostedAt = entry.PostedAt,
                Lines = entry.Lines
                    .Select(line => new JournalEntryLineRow {
                        AccountId = line.AccountId,
                        AccountCode = accounts.TryGetValue(line.AccountId, out var account)
                            ? account.Code
                            : string.Empty,
                        AccountName = accounts.TryGetValue(line.AccountId, out var named)
                            ? named.Name
                            : string.Empty,
                        Description = line.Description,
                        Debit = line.Debit,
                        Credit = line.Credit
                    })
                    .ToList()
            });
        }
    }
}
