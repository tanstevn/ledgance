using Ledgance.Accounting.Ledger.Application.Activity;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/activity")]
    [ApiController]
    public class AccountingActivityController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingActivityController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<Result<IEnumerable<ActivityRow>>> GetRecent(
            [FromQuery] int limit, CancellationToken ct)
            => await _mediator.SendAsync(new GetRecentAccountingActivityQuery {
                Limit = limit > 0 ? limit : 10
            }, ct);
    }
}
