using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers {
    [Route("api/onboarding")]
    [ApiController]
    public class OnboardingController : ControllerBase {
        private readonly IMediator _mediator;

        public OnboardingController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost("organization")]
        public async Task<Result<ProvisionOrganizationCommandResult>> ProvisionOrganization(
            [FromBody] ProvisionOrganizationCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);
    }
}
