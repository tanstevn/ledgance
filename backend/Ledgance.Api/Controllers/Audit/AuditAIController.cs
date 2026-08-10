using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Agent;
using Ledgance.Audit.AI.Application.Analysis;
using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Application.Drafting;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/ai")]
    [ApiController]
    public class AuditAIController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditAIController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("capabilities")]
        public async Task<Result<IEnumerable<AuditAiCapabilityRow>>> GetCapabilities(
            CancellationToken ct)
            => await _mediator.SendAsync(new GetAuditAiCapabilitiesQuery(), ct);

        [HttpPost("assistant")]
        public async Task<Result<AiProposalResult>> Ask(
            [FromBody] AskAuditAssistantCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("engagements/{engagementId:guid}/summarize")]
        public async Task<Result<AiProposalResult>> Summarize(Guid engagementId,
            [FromBody] SummarizeDocumentCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/suggest-risks")]
        public async Task<Result<AiProposalResult>> SuggestRisks(Guid engagementId,
            [FromBody] SuggestRisksCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/suggest-procedures")]
        public async Task<Result<AiProposalResult>> SuggestProcedures(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new SuggestProceduresCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/draft-working-paper")]
        public async Task<Result<AiProposalResult>> DraftWorkingPaper(Guid engagementId,
            [FromBody] DraftWorkingPaperCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/draft-finding")]
        public async Task<Result<AiProposalResult>> DraftFinding(Guid engagementId,
            [FromBody] DraftFindingCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/analyze-risks")]
        public async Task<Result<AiProposalResult>> AnalyzeRisks(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new AnalyzeRisksCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/detect-anomalies")]
        public async Task<Result<AiProposalResult>> DetectAnomalies(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new DetectAnomaliesCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/assist-review")]
        public async Task<Result<AiProposalResult>> AssistReview(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new AssistReviewCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/draft-report")]
        public async Task<Result<AiProposalResult>> DraftReport(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new DraftAuditReportCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/agent")]
        public async Task<Result<AgentRunReport>> RunAgent(Guid engagementId,
            [FromBody] RunAuditAgentCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }
    }
}
