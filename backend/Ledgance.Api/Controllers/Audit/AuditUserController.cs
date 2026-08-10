using Ledgance.Audit.User.Application.Queries;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/users")]
    [ApiController]
    public class AuditUserController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditUserController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<Result<IEnumerable<OrganizationMemberRow>>> GetOrganizationMembers(
            CancellationToken ct)
            => await _mediator.SendAsync(new GetOrganizationMembersQuery(), ct);
    }
}
