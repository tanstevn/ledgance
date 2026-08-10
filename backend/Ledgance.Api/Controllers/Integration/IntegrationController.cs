using Ledgance.Integration.AccountingContext;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Integration {
    [Route("api/integration/accounting-link")]
    [ApiController]
    public class IntegrationController : ControllerBase {
        private readonly IMediator _mediator;

        public IntegrationController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<Result<AccountingLinkStatusView>> GetStatus(CancellationToken ct)
            => await _mediator.SendAsync(new GetAccountingLinkStatusQuery(), ct);

        [HttpPut]
        public async Task<Result<bool>> Set([FromBody] SetAccountingLinkCommand command,
            CancellationToken ct)
            => await _mediator.SendAsync(command, ct);
    }
}
