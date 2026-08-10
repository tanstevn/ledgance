using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Application.Fieldwork;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/engagements/{engagementId:guid}")]
    [ApiController]
    public class AuditFieldworkController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditFieldworkController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost("risks")]
        public async Task<Result<Guid>> AddRisk(Guid engagementId,
            [FromBody] AddRiskCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPut("risks/{riskId:guid}")]
        public async Task<Result<bool>> UpdateRisk(Guid engagementId, Guid riskId,
            [FromBody] UpdateRiskCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.RiskId = riskId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("risks")]
        public async Task<Result<IEnumerable<RiskRow>>> GetRisks(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetRisksQuery { EngagementId = engagementId }, ct);

        [HttpPost("procedures")]
        public async Task<Result<Guid>> AddProcedure(Guid engagementId,
            [FromBody] AddProcedureCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("procedures/{procedureId:guid}")]
        public async Task<Result<string>> UpdateProcedure(Guid engagementId, Guid procedureId,
            [FromBody] UpdateProcedureCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.ProcedureId = procedureId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("procedures")]
        public async Task<Result<IEnumerable<ProcedureRow>>> GetProcedures(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetProceduresQuery { EngagementId = engagementId }, ct);

        [HttpPost("trial-balance")]
        public async Task<Result<ImportTrialBalanceResult>> ImportTrialBalance(
            Guid engagementId, [FromBody] ImportTrialBalanceCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("trial-balance")]
        public async Task<Result<TrialBalanceView>> GetTrialBalance(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetTrialBalanceQuery { EngagementId = engagementId }, ct);
    }
}
