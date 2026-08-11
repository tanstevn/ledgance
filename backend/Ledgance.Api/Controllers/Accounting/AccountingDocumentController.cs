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

        /// <summary>
        /// Accepts one or many files; each is its own command through the full pipeline so
        /// per-file rules apply. A failure stops the batch and names the file, keeping what
        /// already uploaded.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(150 * 1024 * 1024)]
        public async Task<Result<List<Guid>>> Upload(Guid entityId,
            [FromForm] List<IFormFile> files, [FromForm] string? description,
            [FromForm] Guid? journalEntryId, [FromForm] Guid? reconciliationId,
            CancellationToken ct) {
            if (files.Count == 0) {
                return Result<List<Guid>>.Error("No files were provided.");
            }

            var uploaded = new List<Guid>();

            foreach (var file in files) {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);

                var result = await _mediator.SendAsync(new UploadDocumentCommand {
                    EntityId = entityId,
                    JournalEntryId = journalEntryId,
                    ReconciliationId = reconciliationId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Description = description ?? string.Empty,
                    Content = stream.ToArray()
                }, ct);

                if (!result.Successful) {
                    return Result<List<Guid>>.MultipleErrors((result.Errors ?? [])
                        .Select(error => $"{file.FileName}: {error}")
                        .Concat(uploaded.Count > 0
                            ? [$"{uploaded.Count} earlier file(s) were uploaded successfully."]
                            : Array.Empty<string>()));
                }

                uploaded.Add(result.Data);
            }

            return Result<List<Guid>>.Success(uploaded);
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
