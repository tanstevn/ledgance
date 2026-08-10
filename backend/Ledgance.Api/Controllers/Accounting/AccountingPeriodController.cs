using Ledgance.Accounting.Ledger.Application.Periods;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}/periods")]
    [ApiController]
    public class AccountingPeriodController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingPeriodController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Create(Guid entityId,
            [FromBody] CreateFiscalPeriodCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<FiscalPeriodRow>>> List(Guid entityId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetFiscalPeriodsQuery { EntityId = entityId }, ct);

        [HttpPost("{periodId:guid}/close")]
        public async Task<Result<bool>> Close(Guid entityId, Guid periodId,
            CancellationToken ct)
            => await _mediator.SendAsync(new CloseFiscalPeriodCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpPost("{periodId:guid}/reopen")]
        public async Task<Result<bool>> Reopen(Guid entityId, Guid periodId,
            CancellationToken ct)
            => await _mediator.SendAsync(new ReopenFiscalPeriodCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);
    }
}
