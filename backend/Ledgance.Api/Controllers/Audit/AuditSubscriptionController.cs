using Ledgance.Audit.Engagement.Application.Subscription;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/subscription")]
    [ApiController]
    public class AuditSubscriptionController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditSubscriptionController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("usage")]
        public async Task<Result<AuditPlanUsage>> GetUsage(CancellationToken ct)
            => await _mediator.SendAsync(new GetAuditPlanUsageQuery(), ct);
    }
}
