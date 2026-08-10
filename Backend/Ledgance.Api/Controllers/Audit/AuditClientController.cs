using Ledgance.Audit.Client.Application.Commands;
using Ledgance.Audit.Client.Application.Queries;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/clients")]
    [ApiController]
    public class AuditClientController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditClientController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<CreateClientCommandResult>> CreateClient(
            [FromBody] CreateClientCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPut("{id:guid}")]
        public async Task<Result<bool>> UpdateClient(Guid id,
            [FromBody] UpdateClientCommand command, CancellationToken ct) {
            command.Id = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{id:guid}/archive")]
        public async Task<Result<bool>> ArchiveClient(Guid id, CancellationToken ct)
            => await _mediator.SendAsync(new ArchiveClientCommand { Id = id }, ct);

        [HttpGet]
        public async Task<Result<IEnumerable<GetClientsQueryResult>>> GetClients(
            [FromQuery] bool includeArchived, CancellationToken ct)
            => await _mediator.SendAsync(
                new GetClientsQuery { IncludeArchived = includeArchived }, ct);

        [HttpGet("{id:guid}")]
        public async Task<Result<GetClientInfoByIdQueryResult>> GetClientById(Guid id,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetClientInfoByIdQuery { Id = id }, ct);

        [HttpGet("paged")]
        public async Task<PaginatedResult<GetPaginatedClientsQueryRow>> GetPaginatedClients(
            [FromQuery] GetPaginatedClientsQuery query, CancellationToken ct)
            => await _mediator.SendAsync(query, ct);
    }
}
