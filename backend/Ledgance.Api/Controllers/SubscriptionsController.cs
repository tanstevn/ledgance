using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers {
    [Route("api/subscriptions")]
    [ApiController]
    public class SubscriptionsController : ControllerBase {
        private readonly IMediator _mediator;

        public SubscriptionsController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<Result<IEnumerable<SubscriptionPlanRow>>> GetPlans(
            CancellationToken ct)
            => await _mediator.SendAsync(new GetSubscriptionPlansQuery(), ct);
    }
}
