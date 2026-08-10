using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Reconciliations {
    internal static class ReconciliationRules {
        /// <summary>
        /// The cleared balance is the natural-signed sum of the cleared ledger lines, so it
        /// compares directly against the statement balance for the account.
        /// </summary>
        public static decimal ClearedBalance(Account account,
            IReadOnlyList<PostedLedgerLine> lines, IReadOnlyCollection<Guid> clearedEntryIds) =>
            lines
                .Where(line => clearedEntryIds.Contains(line.EntryId))
                .Sum(line => Account.NaturalBalance(account.Type, line.Debit, line.Credit));
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class StartReconciliationCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public Guid AccountId { get; set; }
        public DateOnly StatementDate { get; set; }
        public decimal StatementBalance { get; set; }
    }

    public class StartReconciliationCommandValidator
        : AbstractValidator<StartReconciliationCommand> {
        public StartReconciliationCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.AccountId).NotEmpty();
            RuleFor(x => x.StatementDate).NotEmpty();
        }
    }

    public class StartReconciliationCommandHandler
        : IRequestHandler<StartReconciliationCommand, Result<Guid>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public StartReconciliationCommandHandler(IReconciliationRepository reconciliations,
            IAccountRepository accounts, IEntityGuard guard, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _reconciliations = reconciliations;
            _accounts = accounts;
            _guard = guard;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(StartReconciliationCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var account = await _accounts.FindAsync(request.AccountId, ct);

            if (account is null || account.EntityId != request.EntityId) {
                return Result<Guid>.Error("The account was not found.");
            }

            if (await _reconciliations.HasInProgressForAccountAsync(account.Id, ct)) {
                return Result<Guid>.Error(
                    "A reconciliation is already in progress for this account.");
            }

            var reconciliation = Reconciliation.Start(request.EntityId, request.AccountId,
                request.StatementDate, request.StatementBalance,
                _currentUser.Require().UserId);

            await _reconciliations.AddAsync(reconciliation, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "reconciliation.started", "Reconciliation", reconciliation.Id,
                $"Reconciliation of account {account.Code} '{account.Name}' as of {request.StatementDate:yyyy-MM-dd} was started.",
                request.EntityId), ct);

            return Result<Guid>.Success(reconciliation.Id);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class SetClearedLinesCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid ReconciliationId { get; set; }
        public List<Guid> ClearedEntryIds { get; set; } = [];
    }

    public class SetClearedLinesCommandValidator : AbstractValidator<SetClearedLinesCommand> {
        public SetClearedLinesCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.ReconciliationId).NotEmpty();
        }
    }

    public class SetClearedLinesCommandHandler
        : IRequestHandler<SetClearedLinesCommand, Result<bool>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IEntityGuard _guard;

        public SetClearedLinesCommandHandler(IReconciliationRepository reconciliations,
            ILedgerLineRepository ledgerLines, IEntityGuard guard) {
            _reconciliations = reconciliations;
            _ledgerLines = ledgerLines;
            _guard = guard;
        }

        public async Task<Result<bool>> HandleAsync(SetClearedLinesCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var reconciliation = await _reconciliations.FindAsync(request.ReconciliationId, ct);

            if (reconciliation is null || reconciliation.EntityId != request.EntityId) {
                return Result<bool>.Error("The reconciliation was not found.");
            }

            var lines = await _ledgerLines.ListByAccountAsync(request.EntityId,
                reconciliation.AccountId, null, reconciliation.StatementDate, ct);
            var eligible = lines.Select(line => line.EntryId).ToHashSet();

            if (request.ClearedEntryIds.Any(id => !eligible.Contains(id))) {
                return Result<bool>.Error(
                    "Only ledger lines of this account dated on or before the statement date can be cleared.");
            }

            reconciliation.SetClearedLines(request.ClearedEntryIds);
            await _reconciliations.UpdateAsync(reconciliation, ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class CompleteReconciliationCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid ReconciliationId { get; set; }
        public string? Explanation { get; set; }
    }

    public class CompleteReconciliationCommandValidator
        : AbstractValidator<CompleteReconciliationCommand> {
        public CompleteReconciliationCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.ReconciliationId).NotEmpty();
            RuleFor(x => x.Explanation).MaximumLength(1000);
        }
    }

    public class CompleteReconciliationCommandHandler
        : IRequestHandler<CompleteReconciliationCommand, Result<bool>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public CompleteReconciliationCommandHandler(IReconciliationRepository reconciliations,
            ILedgerLineRepository ledgerLines, IAccountRepository accounts, IEntityGuard guard,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _reconciliations = reconciliations;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _guard = guard;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(CompleteReconciliationCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var reconciliation = await _reconciliations.FindAsync(request.ReconciliationId, ct);

            if (reconciliation is null || reconciliation.EntityId != request.EntityId) {
                return Result<bool>.Error("The reconciliation was not found.");
            }

            var account = await _accounts.FindAsync(reconciliation.AccountId, ct);

            if (account is null) {
                return Result<bool>.Error("The reconciled account was not found.");
            }

            var lines = await _ledgerLines.ListByAccountAsync(request.EntityId,
                reconciliation.AccountId, null, reconciliation.StatementDate, ct);
            var clearedBalance = ReconciliationRules.ClearedBalance(account, lines,
                reconciliation.ClearedLineIds);

            reconciliation.Complete(clearedBalance, request.Explanation,
                _currentUser.Require().UserId);
            await _reconciliations.UpdateAsync(reconciliation, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "reconciliation.completed", "Reconciliation", reconciliation.Id,
                $"Reconciliation of account {account.Code} '{account.Name}' was completed (difference {reconciliation.Difference:0.00}).",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class CancelReconciliationCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid ReconciliationId { get; set; }
    }

    public class CancelReconciliationCommandValidator
        : AbstractValidator<CancelReconciliationCommand> {
        public CancelReconciliationCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.ReconciliationId).NotEmpty();
        }
    }

    public class CancelReconciliationCommandHandler
        : IRequestHandler<CancelReconciliationCommand, Result<bool>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public CancelReconciliationCommandHandler(IReconciliationRepository reconciliations,
            IEntityGuard guard, IActivityRecorder activity) {
            _reconciliations = reconciliations;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(CancelReconciliationCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var reconciliation = await _reconciliations.FindAsync(request.ReconciliationId, ct);

            if (reconciliation is null || reconciliation.EntityId != request.EntityId) {
                return Result<bool>.Error("The reconciliation was not found.");
            }

            reconciliation.Cancel();
            await _reconciliations.UpdateAsync(reconciliation, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting",
                "reconciliation.cancelled", "Reconciliation", reconciliation.Id,
                "The reconciliation was cancelled.", request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    public class ReconciliationRow {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public DateOnly StatementDate { get; set; }
        public decimal StatementBalance { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? ClearedBalance { get; set; }
        public decimal? Difference { get; set; }
        public string? Explanation { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetReconciliationsQuery : IQuery<Result<IEnumerable<ReconciliationRow>>> {
        public Guid EntityId { get; set; }
        public Guid? AccountId { get; set; }
    }

    public class GetReconciliationsQueryHandler
        : IRequestHandler<GetReconciliationsQuery, Result<IEnumerable<ReconciliationRow>>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;

        public GetReconciliationsQueryHandler(IReconciliationRepository reconciliations,
            IAccountRepository accounts, IEntityGuard guard) {
            _reconciliations = reconciliations;
            _accounts = accounts;
            _guard = guard;
        }

        public async Task<Result<IEnumerable<ReconciliationRow>>> HandleAsync(
            GetReconciliationsQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var reconciliations = await _reconciliations.ListAsync(request.EntityId,
                request.AccountId, ct);
            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);

            return Result<IEnumerable<ReconciliationRow>>.Success(reconciliations
                .OrderByDescending(reconciliation => reconciliation.StartedAt)
                .Select(reconciliation => {
                    var account = accounts.GetValueOrDefault(reconciliation.AccountId);

                    return new ReconciliationRow {
                        Id = reconciliation.Id,
                        AccountId = reconciliation.AccountId,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty,
                        StatementDate = reconciliation.StatementDate,
                        StatementBalance = reconciliation.StatementBalance,
                        Status = reconciliation.Status.ToString(),
                        ClearedBalance = reconciliation.ClearedBalance,
                        Difference = reconciliation.Difference,
                        Explanation = reconciliation.Explanation,
                        StartedAt = reconciliation.StartedAt,
                        CompletedAt = reconciliation.CompletedAt
                    };
                }));
        }
    }

    public class ReconciliationLineRow {
        public Guid EntryId { get; set; }
        public long EntryNumber { get; set; }
        public DateOnly EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public bool IsCleared { get; set; }
    }

    public class ReconciliationDetail : ReconciliationRow {
        public decimal WorkingClearedBalance { get; set; }
        public decimal WorkingDifference { get; set; }
        public List<ReconciliationLineRow> Lines { get; set; } = [];
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetReconciliationQuery : IQuery<Result<ReconciliationDetail>> {
        public Guid EntityId { get; set; }
        public Guid ReconciliationId { get; set; }
    }

    public class GetReconciliationQueryHandler
        : IRequestHandler<GetReconciliationQuery, Result<ReconciliationDetail>> {
        private readonly IReconciliationRepository _reconciliations;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;

        public GetReconciliationQueryHandler(IReconciliationRepository reconciliations,
            ILedgerLineRepository ledgerLines, IAccountRepository accounts,
            IEntityGuard guard) {
            _reconciliations = reconciliations;
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _guard = guard;
        }

        public async Task<Result<ReconciliationDetail>> HandleAsync(
            GetReconciliationQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var reconciliation = await _reconciliations.FindAsync(request.ReconciliationId, ct);

            if (reconciliation is null || reconciliation.EntityId != request.EntityId) {
                return Result<ReconciliationDetail>.Error("The reconciliation was not found.");
            }

            var account = await _accounts.FindAsync(reconciliation.AccountId, ct);

            if (account is null) {
                return Result<ReconciliationDetail>.Error("The reconciled account was not found.");
            }

            var lines = await _ledgerLines.ListByAccountAsync(request.EntityId,
                reconciliation.AccountId, null, reconciliation.StatementDate, ct);
            var cleared = reconciliation.ClearedLineIds.ToHashSet();
            var workingBalance = ReconciliationRules.ClearedBalance(account, lines, cleared);

            return Result<ReconciliationDetail>.Success(new ReconciliationDetail {
                Id = reconciliation.Id,
                AccountId = reconciliation.AccountId,
                AccountCode = account.Code,
                AccountName = account.Name,
                StatementDate = reconciliation.StatementDate,
                StatementBalance = reconciliation.StatementBalance,
                Status = reconciliation.Status.ToString(),
                ClearedBalance = reconciliation.ClearedBalance,
                Difference = reconciliation.Difference,
                Explanation = reconciliation.Explanation,
                StartedAt = reconciliation.StartedAt,
                CompletedAt = reconciliation.CompletedAt,
                WorkingClearedBalance = workingBalance,
                WorkingDifference = reconciliation.StatementBalance - workingBalance,
                Lines = lines
                    .OrderBy(line => line.EntryDate)
                    .ThenBy(line => line.EntryNumber)
                    .Select(line => new ReconciliationLineRow {
                        EntryId = line.EntryId,
                        EntryNumber = line.EntryNumber,
                        EntryDate = line.EntryDate,
                        Description = line.Description,
                        Debit = line.Debit,
                        Credit = line.Credit,
                        IsCleared = cleared.Contains(line.EntryId)
                    })
                    .ToList()
            });
        }
    }
}
