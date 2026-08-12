using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Agent;
using Ledgance.Audit.AI.Application.Analysis;
using Ledgance.Audit.AI.Application.Assistant;
using Ledgance.Audit.AI.Application.Drafting;
using Ledgance.Audit.AI.Application.Planning;
using Ledgance.Audit.AI.Application.Portfolio;
using Ledgance.Audit.AI.Application.Reporting;
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

        [HttpPost("engagements/{engagementId:guid}/summarize-findings")]
        public async Task<Result<AiProposalResult>> SummarizeFindings(Guid engagementId,
            [FromBody] SummarizeFindingsCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/summarize-engagement")]
        public async Task<Result<AiProposalResult>> SummarizeEngagement(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new SummarizeEngagementCommand { EngagementId = engagementId }, ct);

        [HttpPost("engagements/{engagementId:guid}/draft-note")]
        public async Task<Result<AiProposalResult>> DraftNote(Guid engagementId,
            [FromBody] DraftEngagementNoteCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/improve-wording")]
        public async Task<Result<AiProposalResult>> ImproveWording(Guid engagementId,
            [FromBody] ImproveWorkingPaperWordingCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/assist-plan")]
        public async Task<Result<AiProposalResult>> AssistPlan(Guid engagementId,
            [FromBody] AssistAuditPlanCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/assist-materiality")]
        public async Task<Result<AiProposalResult>> AssistMateriality(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new AssistMaterialityCommand { EngagementId = engagementId }, ct);

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

        [HttpPost("engagements/{engagementId:guid}/analyze-engagement")]
        public async Task<Result<AiProposalResult>> AnalyzeEngagement(Guid engagementId,
            [FromBody] AnalyzeEngagementCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/analyze-evidence")]
        public async Task<Result<AiProposalResult>> AnalyzeEvidence(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new AnalyzeEvidenceCommand { EngagementId = engagementId }, ct);

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

        [HttpPost("engagements/{engagementId:guid}/report-section")]
        public async Task<Result<AiProposalResult>> GenerateReportSection(Guid engagementId,
            [FromBody] GenerateReportSectionCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/draft-report")]
        public async Task<Result<GeneratedReportView>> GenerateDraftReport(Guid engagementId,
            [FromBody] GenerateDraftReportCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/engagement-report")]
        public async Task<Result<GeneratedReportView>> GenerateEngagementReport(
            Guid engagementId, [FromBody] GenerateEngagementReportCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/agentic-report")]
        public async Task<Result<AgenticReportResult>> RunAgenticReport(Guid engagementId,
            [FromBody] RunAgenticReportWorkflowCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet("engagements/{engagementId:guid}/generated-reports")]
        public async Task<Result<IEnumerable<GeneratedReportView>>> GetGeneratedReports(
            Guid engagementId, CancellationToken ct)
            => await _mediator.SendAsync(
                new GetGeneratedReportsQuery { EngagementId = engagementId }, ct);

        [HttpGet("engagements/{engagementId:guid}/generated-reports/{reportId:guid}")]
        public async Task<Result<GeneratedReportView>> GetGeneratedReport(Guid engagementId,
            Guid reportId, CancellationToken ct)
            => await _mediator.SendAsync(new GetGeneratedReportByIdQuery {
                EngagementId = engagementId,
                ReportId = reportId
            }, ct);

        [HttpPost("engagements/{engagementId:guid}/generated-reports/{reportId:guid}/sections")]
        public async Task<Result<GeneratedReportView>> RegenerateSection(Guid engagementId,
            Guid reportId, [FromBody] RegenerateReportSectionCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            command.ReportId = reportId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("engagements/{engagementId:guid}/generated-reports/{reportId:guid}/consistency")]
        public async Task<Result<AiProposalResult>> CheckConsistency(Guid engagementId,
            Guid reportId, CancellationToken ct)
            => await _mediator.SendAsync(new CheckReportConsistencyCommand {
                EngagementId = engagementId,
                ReportId = reportId
            }, ct);

        [HttpPost("engagements/{engagementId:guid}/generated-reports/{reportId:guid}/review")]
        public async Task<Result<GeneratedReportView>> ReviewGeneratedReport(Guid engagementId,
            Guid reportId, [FromBody] ReviewGeneratedReportCommand command,
            CancellationToken ct) {
            command.EngagementId = engagementId;
            command.ReportId = reportId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("portfolio/analyze")]
        public async Task<Result<AiProposalResult>> AnalyzePortfolio(
            [FromBody] AnalyzePortfolioCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("portfolio/report")]
        public async Task<Result<AiProposalResult>> GeneratePortfolioReport(
            [FromBody] GeneratePortfolioReportCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("engagements/{engagementId:guid}/agent")]
        public async Task<Result<AgentRunReport>> RunAgent(Guid engagementId,
            [FromBody] RunAuditAgentCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }
    }
}
