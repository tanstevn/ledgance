using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers {
    [Route("api/organization")]
    [ApiController]
    public class OrganizationController : ControllerBase {
        private readonly IMediator _mediator;

        public OrganizationController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost("products")]
        public async Task<Result<bool>> EnableProduct(
            [FromBody] EnableOrganizationProductCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);
    }
}
