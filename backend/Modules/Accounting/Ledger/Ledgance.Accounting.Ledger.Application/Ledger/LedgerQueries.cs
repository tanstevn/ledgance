using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Ledger {
    public class GeneralLedgerLineRow {
        public Guid EntryId { get; set; }
        public long EntryNumber { get; set; }
        public DateOnly EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class GeneralLedgerView {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string NormalBalance { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<GeneralLedgerLineRow> Lines { get; set; } = [];
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetGeneralLedgerQuery : IQuery<Result<GeneralLedgerView>> {
        public Guid EntityId { get; set; }
        public Guid AccountId { get; set; }
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
    }

    public class GetGeneralLedgerQueryValidator : AbstractValidator<GetGeneralLedgerQuery> {
        public GetGeneralLedgerQueryValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.AccountId).NotEmpty();
        }
    }

    public class GetGeneralLedgerQueryHandler
        : IRequestHandler<GetGeneralLedgerQuery, Result<GeneralLedgerView>> {
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IEntityGuard _guard;

        public GetGeneralLedgerQueryHandler(ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IEntityGuard guard) {
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _guard = guard;
        }

        public async Task<Result<GeneralLedgerView>> HandleAsync(GetGeneralLedgerQuery request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var account = await _accounts.FindAsync(request.AccountId, ct);

            if (account is null || account.EntityId != request.EntityId) {
                return Result<GeneralLedgerView>.Error("The account was not found.");
            }

            var lines = await _ledgerLines.ListByAccountAsync(request.EntityId,
                request.AccountId, null, request.To, ct);

            var ordered = lines
                .OrderBy(line => line.EntryDate)
                .ThenBy(line => line.EntryNumber)
                .ToList();

            var opening = ordered
                .Where(line => request.From is not null && line.EntryDate < request.From)
                .Aggregate(0m, (balance, line) => balance
                    + Signed(account.NormalBalance, line.Debit, line.Credit));

            var running = opening;
            var rows = new List<GeneralLedgerLineRow>();

            foreach (var line in ordered.Where(line =>
                         request.From is null || line.EntryDate >= request.From)) {
                running += Signed(account.NormalBalance, line.Debit, line.Credit);

                rows.Add(new GeneralLedgerLineRow {
                    EntryId = line.EntryId,
                    EntryNumber = line.EntryNumber,
                    EntryDate = line.EntryDate,
                    Description = line.Description,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    RunningBalance = running
                });
            }

            return Result<GeneralLedgerView>.Success(new GeneralLedgerView {
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.Name,
                NormalBalance = account.NormalBalance.ToString(),
                OpeningBalance = opening,
                ClosingBalance = running,
                Lines = rows
            });
        }

        private static decimal Signed(BalanceSide normalBalance, decimal debit, decimal credit) =>
            normalBalance == BalanceSide.Debit ? debit - credit : credit - debit;
    }

    public class TrialBalanceRow {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
    }

    public class TrialBalanceView {
        public DateOnly AsOf { get; set; }
        public List<TrialBalanceRow> Rows { get; set; } = [];
        public decimal TotalDebitBalances { get; set; }
        public decimal TotalCreditBalances { get; set; }
        public bool IsBalanced { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetTrialBalanceQuery : IQuery<Result<TrialBalanceView>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class GetTrialBalanceQueryValidator : AbstractValidator<GetTrialBalanceQuery> {
        public GetTrialBalanceQueryValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class GetTrialBalanceQueryHandler
        : IRequestHandler<GetTrialBalanceQuery, Result<TrialBalanceView>> {
        private readonly ILedgerLineRepository _ledgerLines;
        private readonly IAccountRepository _accounts;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;

        public GetTrialBalanceQueryHandler(ILedgerLineRepository ledgerLines,
            IAccountRepository accounts, IFiscalPeriodRepository periods, IEntityGuard guard) {
            _ledgerLines = ledgerLines;
            _accounts = accounts;
            _periods = periods;
            _guard = guard;
        }

        public async Task<Result<TrialBalanceView>> HandleAsync(GetTrialBalanceQuery request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<TrialBalanceView>.Error("The fiscal period was not found.");
            }

            var accounts = (await _accounts.ListAsync(request.EntityId, ct))
                .ToDictionary(account => account.Id);
            var lines = await _ledgerLines.ListForEntityAsync(request.EntityId, null,
                period.EndDate, ct);

            var rows = lines
                .GroupBy(line => line.AccountId)
                .Select(group => {
                    var account = accounts.GetValueOrDefault(group.Key);
                    var debits = group.Sum(line => line.Debit);
                    var credits = group.Sum(line => line.Credit);
                    var net = debits - credits;

                    return new TrialBalanceRow {
                        AccountId = group.Key,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty,
                        Type = account?.Type.ToString() ?? string.Empty,
                        TotalDebits = debits,
                        TotalCredits = credits,
                        DebitBalance = net > 0 ? net : 0,
                        CreditBalance = net < 0 ? -net : 0
                    };
                })
                .Where(row => row.TotalDebits != 0 || row.TotalCredits != 0)
                .OrderBy(row => row.AccountCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var totalDebits = rows.Sum(row => row.DebitBalance);
            var totalCredits = rows.Sum(row => row.CreditBalance);

            return Result<TrialBalanceView>.Success(new TrialBalanceView {
                AsOf = period.EndDate,
                Rows = rows,
                TotalDebitBalances = totalDebits,
                TotalCreditBalances = totalCredits,
                IsBalanced = totalDebits == totalCredits
            });
        }
    }
}
