using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit")]
    [ApiController]
    public class AuditAccountingContextController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditAccountingContextController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("accounting-context")]
        public async Task<Result<LinkedAccountingContextView>> GetLinkedContext(
            CancellationToken ct)
            => await _mediator.SendAsync(new GetLinkedAccountingContextQuery(), ct);

        [HttpPost("engagements/{engagementId:guid}/trial-balance/from-accounting")]
        public async Task<Result<ImportTrialBalanceResult>> ImportFromAccounting(
            Guid engagementId, [FromBody] ImportTrialBalanceFromAccountingCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }
    }
}
