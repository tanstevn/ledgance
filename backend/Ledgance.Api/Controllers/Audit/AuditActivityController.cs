using Ledgance.Audit.Engagement.Application.Activity;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/activity")]
    [ApiController]
    public class AuditActivityController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditActivityController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<Result<IEnumerable<ActivityRow>>> GetRecent(
            [FromQuery] int limit, CancellationToken ct)
            => await _mediator.SendAsync(new GetRecentAuditActivityQuery {
                Limit = limit > 0 ? limit : 10
            }, ct);
    }
}
