using Ledgance.Accounting.Ledger.Application.ChartOfAccounts;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}/accounts")]
    [ApiController]
    public class AccountingAccountController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingAccountController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Create(Guid entityId,
            [FromBody] CreateAccountCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<AccountRow>>> List(Guid entityId,
            [FromQuery] bool includeInactive, CancellationToken ct)
            => await _mediator.SendAsync(new GetChartOfAccountsQuery {
                EntityId = entityId,
                IncludeInactive = includeInactive
            }, ct);

        [HttpPut("{accountId:guid}")]
        public async Task<Result<bool>> Update(Guid entityId, Guid accountId,
            [FromBody] UpdateAccountCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            command.AccountId = accountId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{accountId:guid}/deactivate")]
        public async Task<Result<bool>> Deactivate(Guid entityId, Guid accountId,
            CancellationToken ct)
            => await _mediator.SendAsync(new SetAccountActiveCommand {
                EntityId = entityId,
                AccountId = accountId,
                Active = false
            }, ct);

        [HttpPost("{accountId:guid}/reactivate")]
        public async Task<Result<bool>> Reactivate(Guid entityId, Guid accountId,
            CancellationToken ct)
            => await _mediator.SendAsync(new SetAccountActiveCommand {
                EntityId = entityId,
                AccountId = accountId,
                Active = true
            }, ct);
    }
}
