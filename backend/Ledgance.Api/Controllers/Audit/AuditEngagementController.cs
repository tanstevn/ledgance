using Ledgance.Audit.Engagement.Application.Activity;
using Ledgance.Audit.Engagement.Application.Engagements;
using Ledgance.Audit.Engagement.Application.Planning;
using Ledgance.Audit.Engagement.Application.Team;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/engagements")]
    [ApiController]
    public class AuditEngagementController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditEngagementController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<CreateEngagementCommandResult>> Create(
            [FromBody] CreateEngagementCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpGet]
        public async Task<Result<IEnumerable<EngagementListRow>>> List(
            [FromQuery] Guid? clientId, CancellationToken ct)
            => await _mediator.SendAsync(new GetEngagementsQuery { ClientId = clientId }, ct);

        [HttpGet("{id:guid}")]
        public async Task<Result<EngagementDetail>> GetById(Guid id, CancellationToken ct)
            => await _mediator.SendAsync(new GetEngagementByIdQuery { Id = id }, ct);

        [HttpPut("{id:guid}")]
        public async Task<Result<bool>> Update(Guid id,
            [FromBody] UpdateEngagementCommand command, CancellationToken ct) {
            command.Id = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{id:guid}/status")]
        public async Task<Result<string>> ChangeStatus(Guid id,
            [FromBody] ChangeEngagementStatusCommand command, CancellationToken ct) {
            command.Id = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{id:guid}/team")]
        public async Task<Result<Guid>> AssignTeamMember(Guid id,
            [FromBody] AssignTeamMemberCommand command, CancellationToken ct) {
            command.EngagementId = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpDelete("{id:guid}/team/{memberId:guid}")]
        public async Task<Result<bool>> RemoveTeamMember(Guid id, Guid memberId,
            CancellationToken ct)
            => await _mediator.SendAsync(new RemoveTeamMemberCommand {
                EngagementId = id,
                MemberId = memberId
            }, ct);

        [HttpPut("{id:guid}/plan")]
        public async Task<Result<bool>> SavePlan(Guid id,
            [FromBody] SaveAuditPlanCommand command, CancellationToken ct) {
            command.EngagementId = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{id:guid}/plan/approve")]
        public async Task<Result<bool>> ApprovePlan(Guid id, CancellationToken ct)
            => await _mediator.SendAsync(
                new ApproveAuditPlanCommand { EngagementId = id }, ct);

        [HttpPut("{id:guid}/materiality")]
        public async Task<Result<bool>> SetMateriality(Guid id,
            [FromBody] SetMaterialityCommand command, CancellationToken ct) {
            command.EngagementId = id;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("{id:guid}/activity")]
        public async Task<Result<IEnumerable<ActivityRow>>> GetActivity(Guid id,
            [FromQuery] int limit, CancellationToken ct)
            => await _mediator.SendAsync(new GetEngagementActivityQuery {
                EngagementId = id,
                Limit = limit < 1 ? 50 : limit
            }, ct);
    }
}
