using Ledgance.Accounting.Ledger.Application.Journal;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}/journal-entries")]
    [ApiController]
    public class AccountingJournalController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingJournalController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Create(Guid entityId,
            [FromBody] CreateJournalEntryCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet]
        public async Task<PaginatedResult<JournalEntryRow>> List(Guid entityId,
            [FromQuery] int page, [FromQuery] int pageSize,
            [FromQuery] JournalEntryStatus? status, [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to, CancellationToken ct)
            => await _mediator.SendAsync(new GetJournalEntriesQuery {
                EntityId = entityId,
                Page = page > 0 ? page : 1,
                PageSize = pageSize > 0 ? pageSize : 25,
                Status = status,
                From = from,
                To = to
            }, ct);

        [HttpGet("{entryId:guid}")]
        public async Task<Result<JournalEntryDetail>> Get(Guid entityId, Guid entryId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetJournalEntryQuery {
                EntityId = entityId,
                EntryId = entryId
            }, ct);

        [HttpPut("{entryId:guid}")]
        public async Task<Result<bool>> Update(Guid entityId, Guid entryId,
            [FromBody] UpdateJournalEntryCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            command.EntryId = entryId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpDelete("{entryId:guid}")]
        public async Task<Result<bool>> Delete(Guid entityId, Guid entryId,
            CancellationToken ct)
            => await _mediator.SendAsync(new DeleteJournalEntryCommand {
                EntityId = entityId,
                EntryId = entryId
            }, ct);

        [HttpPost("{entryId:guid}/post")]
        public async Task<Result<bool>> Post(Guid entityId, Guid entryId, CancellationToken ct)
            => await _mediator.SendAsync(new PostJournalEntryCommand {
                EntityId = entityId,
                EntryId = entryId
            }, ct);

        [HttpPost("{entryId:guid}/reverse")]
        public async Task<Result<Guid>> Reverse(Guid entityId, Guid entryId,
            [FromBody] ReverseJournalEntryCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            command.EntryId = entryId;
            return await _mediator.SendAsync(command, ct);
        }
    }
}
