using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.ChartOfAccounts {
    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class CreateAccountCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public string Classification { get; set; } = string.Empty;
        public Guid? ParentAccountId { get; set; }
    }

    public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand> {
        public CreateAccountCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.Classification).MaximumLength(100);
        }
    }

    public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>> {
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public CreateAccountCommandHandler(IAccountRepository accounts, IEntityGuard guard,
            IActivityRecorder activity) {
            _accounts = accounts;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(CreateAccountCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            if (await _accounts.CodeExistsAsync(request.EntityId, request.Code.Trim(),
                    null, ct)) {
                return Result<Guid>.Error(
                    $"Account code '{request.Code.Trim()}' is already in use.");
            }

            Account? parent = null;

            if (request.ParentAccountId is not null) {
                parent = await _accounts.FindAsync(request.ParentAccountId.Value, ct);

                if (parent is null || parent.EntityId != request.EntityId) {
                    return Result<Guid>.Error("The parent account was not found.");
                }
            }

            var account = Account.Open(request.EntityId, request.Code, request.Name,
                request.Type, request.Classification, parent);

            await _accounts.AddAsync(account, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "account.created",
                "Account", account.Id,
                $"created the account {account.Code} {account.Name}.",
                request.EntityId), ct);

            return Result<Guid>.Success(account.Id);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class UpdateAccountCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid AccountId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public string Classification { get; set; } = string.Empty;
    }

    public class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand> {
        public UpdateAccountCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.AccountId).NotEmpty();
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.Classification).MaximumLength(100);
        }
    }

    public class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, Result<bool>> {
        private readonly IAccountRepository _accounts;
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public UpdateAccountCommandHandler(IAccountRepository accounts,
            ILedgerLineRepository ledgerLines, IEntityGuard guard, IActivityRecorder activity) {
            _accounts = accounts;
            _ledgerLines = ledgerLines;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateAccountCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var account = await _accounts.FindAsync(request.AccountId, ct);

            if (account is null || account.EntityId != request.EntityId) {
                return Result<bool>.Error("The account was not found.");
            }

            if (await _accounts.CodeExistsAsync(request.EntityId, request.Code.Trim(),
                    account.Id, ct)) {
                return Result<bool>.Error(
                    $"Account code '{request.Code.Trim()}' is already in use.");
            }

            if (account.Type != request.Type) {
                var hasPostings = await _ledgerLines.HasPostingsAsync(account.Id, ct);
                var hasChildren = await _accounts.HasChildrenAsync(account.Id, ct);
                var parent = account.ParentAccountId is null
                    ? null
                    : await _accounts.FindAsync(account.ParentAccountId.Value, ct);

                account.Reclassify(request.Type, hasPostings, hasChildren, parent);
            }

            account.Rename(request.Code, request.Name, request.Classification);
            await _accounts.UpdateAsync(account, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "account.updated",
                "Account", account.Id,
                $"updated the account {account.Code} {account.Name}.",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class SetAccountActiveCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid AccountId { get; set; }
        public bool Active { get; set; }
    }

    public class SetAccountActiveCommandValidator : AbstractValidator<SetAccountActiveCommand> {
        public SetAccountActiveCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.AccountId).NotEmpty();
        }
    }

    public class SetAccountActiveCommandHandler
        : IRequestHandler<SetAccountActiveCommand, Result<bool>> {
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public SetAccountActiveCommandHandler(IAccountRepository accounts, IEntityGuard guard,
            IActivityRecorder activity) {
            _accounts = accounts;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(SetAccountActiveCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var account = await _accounts.FindAsync(request.AccountId, ct);

            if (account is null || account.EntityId != request.EntityId) {
                return Result<bool>.Error("The account was not found.");
            }

            if (request.Active) {
                account.Reactivate();
            } else {
                account.Deactivate();
            }

            await _accounts.UpdateAsync(account, ct);

            var action = request.Active ? "account.reactivated" : "account.deactivated";
            var verb = request.Active ? "reactivated" : "deactivated";

            await _activity.RecordAsync(new ActivityEntry("Accounting", action,
                "Account", account.Id,
                $"{verb} the account {account.Code} {account.Name}.",
                request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    public class AccountRow {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string NormalBalance { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public Guid? ParentAccountId { get; set; }
        public bool HasChildren { get; set; }
        public bool IsActive { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetChartOfAccountsQuery : IQuery<Result<IEnumerable<AccountRow>>> {
        public Guid EntityId { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public class GetChartOfAccountsQueryHandler
        : IRequestHandler<GetChartOfAccountsQuery, Result<IEnumerable<AccountRow>>> {
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;

        public GetChartOfAccountsQueryHandler(IAccountRepository accounts, IEntityGuard guard) {
            _accounts = accounts;
            _guard = guard;
        }

        public async Task<Result<IEnumerable<AccountRow>>> HandleAsync(
            GetChartOfAccountsQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var accounts = await _accounts.ListAsync(request.EntityId, ct);
            var parents = accounts
                .Where(account => account.ParentAccountId is not null)
                .Select(account => account.ParentAccountId!.Value)
                .ToHashSet();

            return Result<IEnumerable<AccountRow>>.Success(accounts
                .Where(account => request.IncludeInactive || account.IsActive)
                .OrderBy(account => account.Code, StringComparer.OrdinalIgnoreCase)
                .Select(account => new AccountRow {
                    Id = account.Id,
                    Code = account.Code,
                    Name = account.Name,
                    Type = account.Type.ToString(),
                    NormalBalance = account.NormalBalance.ToString(),
                    Classification = account.Classification,
                    ParentAccountId = account.ParentAccountId,
                    HasChildren = parents.Contains(account.Id),
                    IsActive = account.IsActive
                }));
        }
    }
}
