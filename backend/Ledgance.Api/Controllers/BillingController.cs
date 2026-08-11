using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers {
    [Route("api/billing")]
    [ApiController]
    public class BillingController : ControllerBase {
        private readonly IMediator _mediator;

        public BillingController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("overview")]
        public async Task<Result<BillingOverview>> Overview(CancellationToken ct)
            => await _mediator.SendAsync(new GetBillingOverviewQuery(), ct);

        [HttpPost("checkout")]
        public async Task<Result<StartCheckoutResult>> Checkout(
            [FromBody] StartCheckoutCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("portal")]
        public async Task<Result<BillingPortalResult>> Portal(
            [FromBody] CreateBillingPortalSessionCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("change-plan")]
        public async Task<Result<bool>> ChangePlan(
            [FromBody] ChangeSubscriptionPlanCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("cancel")]
        public async Task<Result<bool>> Cancel(
            [FromBody] SetSubscriptionCancellationCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        /// <summary>
        /// The payment provider's callback. Anonymous by necessity — the caller is the provider,
        /// and the payload signature is what authenticates it. A payload that fails verification
        /// is answered with 400 so the provider records the delivery as failed.
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(CancellationToken ct) {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(ct);

            var result = await _mediator.SendAsync(new HandleBillingWebhookCommand {
                Payload = payload,
                Signature = Request.Headers["Stripe-Signature"].ToString()
            }, ct);

            return result.Successful ? Ok(result) : BadRequest(result);
        }
    }
}
