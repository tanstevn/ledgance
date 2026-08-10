using Ledgance.Accounting.Ledger.Application.Reconciliations;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}/reconciliations")]
    [ApiController]
    public class AccountingReconciliationController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingReconciliationController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Start(Guid entityId,
            [FromBody] StartReconciliationCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<ReconciliationRow>>> List(Guid entityId,
            [FromQuery] Guid? accountId, CancellationToken ct)
            => await _mediator.SendAsync(new GetReconciliationsQuery {
                EntityId = entityId,
                AccountId = accountId
            }, ct);

        [HttpGet("{reconciliationId:guid}")]
        public async Task<Result<ReconciliationDetail>> Get(Guid entityId,
            Guid reconciliationId, CancellationToken ct)
            => await _mediator.SendAsync(new GetReconciliationQuery {
                EntityId = entityId,
                ReconciliationId = reconciliationId
            }, ct);

        [HttpPut("{reconciliationId:guid}/cleared-lines")]
        public async Task<Result<bool>> SetClearedLines(Guid entityId, Guid reconciliationId,
            [FromBody] SetClearedLinesCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            command.ReconciliationId = reconciliationId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{reconciliationId:guid}/complete")]
        public async Task<Result<bool>> Complete(Guid entityId, Guid reconciliationId,
            [FromBody] CompleteReconciliationCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            command.ReconciliationId = reconciliationId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{reconciliationId:guid}/cancel")]
        public async Task<Result<bool>> Cancel(Guid entityId, Guid reconciliationId,
            CancellationToken ct)
            => await _mediator.SendAsync(new CancelReconciliationCommand {
                EntityId = entityId,
                ReconciliationId = reconciliationId
            }, ct);
    }
}
