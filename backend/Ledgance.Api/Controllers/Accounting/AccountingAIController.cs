using Ledgance.Accounting.AI.Application;
using Ledgance.Accounting.AI.Application.Analysis;
using Ledgance.Accounting.AI.Application.Assistant;
using Ledgance.Accounting.AI.Application.Suggestions;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/ai")]
    [ApiController]
    public class AccountingAIController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingAIController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpGet("capabilities")]
        public async Task<Result<IEnumerable<AccountingAiCapabilityRow>>> GetCapabilities(
            CancellationToken ct)
            => await _mediator.SendAsync(new GetAccountingAiCapabilitiesQuery(), ct);

        [HttpPost("assistant")]
        public async Task<Result<AiProposalResult>> Ask(
            [FromBody] AskAccountingAssistantCommand command, CancellationToken ct)
            => await _mediator.SendAsync(command, ct);

        [HttpPost("entities/{entityId:guid}/entries/{entryId:guid}/explain")]
        public async Task<Result<AiProposalResult>> ExplainEntry(Guid entityId, Guid entryId,
            CancellationToken ct)
            => await _mediator.SendAsync(new ExplainJournalEntryCommand {
                EntityId = entityId,
                EntryId = entryId
            }, ct);

        [HttpPost("entities/{entityId:guid}/periods/{periodId:guid}/summarize")]
        public async Task<Result<AiProposalResult>> SummarizePeriod(Guid entityId,
            Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new SummarizePeriodCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpPost("entities/{entityId:guid}/suggest-entry")]
        public async Task<Result<AiProposalResult>> SuggestEntry(Guid entityId,
            [FromBody] SuggestJournalEntryCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("entities/{entityId:guid}/reconciliations/{reconciliationId:guid}/assist")]
        public async Task<Result<AiProposalResult>> AssistReconciliation(Guid entityId,
            Guid reconciliationId, CancellationToken ct)
            => await _mediator.SendAsync(new AssistReconciliationCommand {
                EntityId = entityId,
                ReconciliationId = reconciliationId
            }, ct);

        [HttpPost("entities/{entityId:guid}/periods/{periodId:guid}/explain-statements")]
        public async Task<Result<AiProposalResult>> ExplainStatements(Guid entityId,
            Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new ExplainStatementsCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpPost("entities/{entityId:guid}/analyze-variance")]
        public async Task<Result<AiProposalResult>> AnalyzeVariance(Guid entityId,
            [FromBody] AnalyzeVarianceCommand command, CancellationToken ct) {
            command.EntityId = entityId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("entities/{entityId:guid}/periods/{periodId:guid}/detect-anomalies")]
        public async Task<Result<AiProposalResult>> DetectAnomalies(Guid entityId,
            Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new DetectAnomaliesCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpPost("entities/{entityId:guid}/periods/{periodId:guid}/analyze-financials")]
        public async Task<Result<AiProposalResult>> AnalyzeFinancials(Guid entityId,
            Guid periodId, CancellationToken ct)
            => await _mediator.SendAsync(new AnalyzeFinancialsCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);

        [HttpPost("entities/{entityId:guid}/periods/{periodId:guid}/assist-close")]
        public async Task<Result<AiProposalResult>> AssistClose(Guid entityId, Guid periodId,
            CancellationToken ct)
            => await _mediator.SendAsync(new AssistCloseCommand {
                EntityId = entityId,
                PeriodId = periodId
            }, ct);
    }
}
