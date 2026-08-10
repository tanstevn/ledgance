using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Reports {
    public class ReportLineRow {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class IncomeStatementView {
        public string PeriodName { get; set; } = string.Empty;
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public List<ReportLineRow> Revenue { get; set; } = [];
        public List<ReportLineRow> Expenses { get; set; } = [];
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetIncomeStatementQuery : IQuery<Result<IncomeStatementView>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class GetIncomeStatementQueryValidator
        : AbstractValidator<GetIncomeStatementQuery> {
        public GetIncomeStatementQueryValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class GetIncomeStatementQueryHandler
        : IRequestHandler<GetIncomeStatementQuery, Result<IncomeStatementView>> {
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;

        public GetIncomeStatementQueryHandler(ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IFiscalPeriodRepository periods, IEntityGuard guard) {
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
        }

        public async Task<Result<IncomeStatementView>> HandleAsync(
            GetIncomeStatementQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<IncomeStatementView>.Error("The fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(request.EntityId,
                period.StartDate, period.EndDate, ct);

            var revenue = Section(lines, accounts, AccountType.Revenue);
            var expenses = Section(lines, accounts, AccountType.Expense);
            var totalRevenue = revenue.Sum(row => row.Amount);
            var totalExpenses = expenses.Sum(row => row.Amount);

            return Result<IncomeStatementView>.Success(new IncomeStatementView {
                PeriodName = period.Name,
                From = period.StartDate,
                To = period.EndDate,
                Revenue = revenue,
                Expenses = expenses,
                TotalRevenue = totalRevenue,
                TotalExpenses = totalExpenses,
                NetIncome = totalRevenue - totalExpenses
            });
        }

        private static List<ReportLineRow> Section(IReadOnlyList<PostedLedgerLine> lines,
            IReadOnlyDictionary<Guid, Account> accounts, AccountType type) =>
            lines
                .Where(line => accounts.TryGetValue(line.AccountId, out var account)
                    && account.Type == type)
                .GroupBy(line => line.AccountId)
                .Select(group => new ReportLineRow {
                    AccountId = group.Key,
                    AccountCode = accounts[group.Key].Code,
                    AccountName = accounts[group.Key].Name,
                    Amount = Account.NaturalBalance(type,
                        group.Sum(line => line.Debit), group.Sum(line => line.Credit))
                })
                .Where(row => row.Amount != 0)
                .OrderBy(row => row.AccountCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public class BalanceSheetView {
        public DateOnly AsOf { get; set; }
        public List<ReportLineRow> Assets { get; set; } = [];
        public List<ReportLineRow> Liabilities { get; set; } = [];
        public List<ReportLineRow> Equity { get; set; } = [];
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal CurrentEarnings { get; set; }
        public bool IsBalanced { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetBalanceSheetQuery : IQuery<Result<BalanceSheetView>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class GetBalanceSheetQueryValidator : AbstractValidator<GetBalanceSheetQuery> {
        public GetBalanceSheetQueryValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class GetBalanceSheetQueryHandler
        : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetView>> {
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;

        public GetBalanceSheetQueryHandler(ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IFiscalPeriodRepository periods, IEntityGuard guard) {
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
        }

        public async Task<Result<BalanceSheetView>> HandleAsync(GetBalanceSheetQuery request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<BalanceSheetView>.Error("The fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);

            var balances = lines
                .Where(line => accounts.ContainsKey(line.AccountId))
                .GroupBy(line => line.AccountId)
                .Select(group => new {
                    Account = accounts[group.Key],
                    Balance = Account.NaturalBalance(accounts[group.Key].Type,
                        group.Sum(line => line.Debit), group.Sum(line => line.Credit))
                })
                .Where(balance => balance.Balance != 0)
                .ToList();

            List<ReportLineRow> Section(AccountType type) =>
                balances
                    .Where(balance => balance.Account.Type == type)
                    .Select(balance => new ReportLineRow {
                        AccountId = balance.Account.Id,
                        AccountCode = balance.Account.Code,
                        AccountName = balance.Account.Name,
                        Amount = balance.Balance
                    })
                    .OrderBy(row => row.AccountCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var assets = Section(AccountType.Asset);
            var liabilities = Section(AccountType.Liability);
            var equity = Section(AccountType.Equity);

            var totalAssets = assets.Sum(row => row.Amount);
            var totalLiabilities = liabilities.Sum(row => row.Amount);
            var totalEquity = equity.Sum(row => row.Amount);

            // No closing entries exist in the MVP, so life-to-date profit is presented as a
            // current-earnings line inside equity; with it the statement balances exactly.
            var currentEarnings = balances
                .Where(balance => balance.Account.Type
                    is AccountType.Revenue or AccountType.Expense)
                .Sum(balance => balance.Account.Type == AccountType.Revenue
                    ? balance.Balance
                    : -balance.Balance);

            return Result<BalanceSheetView>.Success(new BalanceSheetView {
                AsOf = period.EndDate,
                Assets = assets,
                Liabilities = liabilities,
                Equity = equity,
                TotalAssets = totalAssets,
                TotalLiabilities = totalLiabilities,
                TotalEquity = totalEquity,
                CurrentEarnings = currentEarnings,
                IsBalanced = totalAssets == totalLiabilities + totalEquity + currentEarnings
            });
        }
    }
}
