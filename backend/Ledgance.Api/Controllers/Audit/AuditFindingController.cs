using Ledgance.Audit.Engagement.Application.Findings;
using Ledgance.Audit.Engagement.Application.Reporting;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/engagements/{engagementId:guid}")]
    [ApiController]
    public class AuditFindingController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditFindingController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost("findings")]
        public async Task<Result<Guid>> RaiseFinding(Guid engagementId,
            [FromBody] RaiseFindingCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("findings/{findingId:guid}/status")]
        public async Task<Result<string>> UpdateFindingStatus(Guid engagementId,
            Guid findingId, [FromBody] UpdateFindingStatusCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            command.FindingId = findingId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("findings")]
        public async Task<Result<IEnumerable<FindingRow>>> GetFindings(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetFindingsQuery { EngagementId = engagementId }, ct);

        [HttpPut("report")]
        public async Task<Result<Guid>> SaveReport(Guid engagementId,
            [FromBody] SaveAuditReportCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("report/finalize")]
        public async Task<Result<bool>> FinalizeReport(Guid engagementId, CancellationToken ct)
            => await _mediator.SendAsync(
                new FinalizeAuditReportCommand { EngagementId = engagementId }, ct);

        [HttpGet("report")]
        public async Task<Result<AuditReportView>> GetReport(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetAuditReportQuery { EngagementId = engagementId }, ct);
    }
}
