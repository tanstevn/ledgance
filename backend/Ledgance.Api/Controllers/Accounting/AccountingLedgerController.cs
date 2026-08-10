using Ledgance.Accounting.Ledger.Application.Ledger;
using Ledgance.Accounting.Ledger.Application.Reports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}")]
    [ApiController]
    public class AccountingLedgerController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingLedgerController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("general-ledger")]
        public async Task<Result<GeneralLedgerView>> GetGeneralLedger(Guid entityId,
            [FromQuery] Guid accountId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetGeneralLedgerQuery {
                EntityId = entityId,
                AccountId = accountId,
                From = from,
                To = to
            }, ct);

        [HttpGet("trial-balance")]
        public async Task<Result<TrialBalanceView>> GetTrialBalance(Guid entityId,
            [FromQuery] Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new GetTrialBalanceQuery {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpGet("reports/income-statement")]
        public async Task<Result<IncomeStatementView>> GetIncomeStatement(Guid entityId,
            [FromQuery] Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new GetIncomeStatementQuery {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpGet("reports/balance-sheet")]
        public async Task<Result<BalanceSheetView>> GetBalanceSheet(Guid entityId,
            [FromQuery] Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new GetBalanceSheetQuery {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);
    }
}
