using Ledgance.Audit.Client.Application.Commands;
using Ledgance.Audit.Client.Application.Queries;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit-client")]
    [ApiController]
    public class AuditClientController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditClientController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<CreateClientCommandResult>> CreateClient(
            [FromBody] CreateClientCommand command, 
            CancellationToken ct) 
            => await _mediator.SendAsync(command, ct);

        [HttpGet("info")]
        public async Task<Result<GetClientInfoByIdQueryResult>> GetClientInfoById(
            [FromQuery] GetClientInfoByIdQuery query,
            CancellationToken ct) 
            => await _mediator.SendAsync(query, ct);

        [HttpGet("all")]
        public async Task<Result<IEnumerable<GetClientsQueryResult>>> GetClients(
            CancellationToken ct) 
            => await _mediator.SendAsync(new GetClientsQuery(), ct);
    }
}
