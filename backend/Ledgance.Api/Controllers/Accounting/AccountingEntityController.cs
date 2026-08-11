using Ledgance.Accounting.Ledger.Application.Activity;
using Ledgance.Accounting.Ledger.Application.Entities;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities")]
    [ApiController]
    public class AccountingEntityController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingEntityController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Create([FromBody] CreateEntityCommand command,
            CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpGet]
        public async Task<Result<IEnumerable<EntityRow>>> List(CancellationToken ct)
            => await _mediator.SendAsync(new GetEntitiesQuery(), ct);

        [HttpGet("paged")]
        public async Task<PaginatedResult<PaginatedEntityRow>> ListPaged(
            [FromQuery] GetPaginatedEntitiesQuery query, CancellationToken ct)
            => await _mediator.SendAsync(query, ct);

        [HttpGet("{id:guid}")]
        public async Task<Result<EntityRow>> Get(Guid id, CancellationToken ct)
            => await _mediator.SendAsync(new GetEntityQuery { EntityId = id }, ct);

        [HttpPut("{id:guid}")]
        public async Task<Result<bool>> Update(Guid id, [FromBody] UpdateEntityCommand command,
            CancellationToken ct) {
            command.EntityId = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{id:guid}/archive")]
        public async Task<Result<bool>> Archive(Guid id, CancellationToken ct)
            => await _mediator.SendAsync(new ArchiveEntityCommand { EntityId = id }, ct);

        [HttpGet("{id:guid}/activity")]
        public async Task<Result<IEnumerable<ActivityRow>>> GetActivity(Guid id,
            [FromQuery] int limit, CancellationToken ct)
            => await _mediator.SendAsync(new GetEntityActivityQuery {
                EntityId = id,
                Limit = limit > 0 ? limit : 50
            }, ct);
    }
}
