using Ledgance.Accounting.Ledger.Application.Documents;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Accounting {
    [Route("api/accounting/entities/{entityId:guid}/documents")]
    [ApiController]
    public class AccountingDocumentController : ControllerBase {
        private readonly IMediator _mediator;

        public AccountingDocumentController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<Result<Guid>> Upload(Guid entityId, [FromForm] IFormFile file,
            [FromForm] string? description, [FromForm] Guid? journalEntryId,
            [FromForm] Guid? reconciliationId, CancellationToken ct) {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);

            return await _mediator.SendAsync(new UploadDocumentCommand {
                EntityId = entityId,
                JournalEntryId = journalEntryId,
                ReconciliationId = reconciliationId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Description = description ?? string.Empty,
                Content = stream.ToArray()
            }, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<DocumentRow>>> List(Guid entityId,
            [FromQuery] Guid? journalEntryId, [FromQuery] Guid? reconciliationId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetDocumentsQuery {
                EntityId = entityId,
                JournalEntryId = journalEntryId,
                ReconciliationId = reconciliationId
            }, ct);

        [HttpGet("{documentId:guid}/download-url")]
        public async Task<Result<string>> GetDownloadUrl(Guid entityId, Guid documentId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetDocumentDownloadUrlQuery {
                EntityId = entityId,
                DocumentId = documentId
            }, ct);
    }
}
