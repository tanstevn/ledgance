using Ledgance.Audit.Engagement.Application.Evidence;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/engagements/{engagementId:guid}/evidence")]
    [ApiController]
    public class AuditEvidenceController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditEvidenceController(IMediator mediator) {
            _mediator = mediator;
        }

        /// <summary>
        /// Accepts one or many files. Each file is its own command through the full pipeline,
        /// so per-file rules (size, storage limit, versioning) apply to each; a failure stops
        /// the batch and reports which file refused, keeping the files already stored.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(150 * 1024 * 1024)]
        public async Task<Result<List<Guid>>> Upload(Guid engagementId,
            [FromForm] List<IFormFile> files, [FromForm] string? description,
            [FromForm] string? category, [FromForm] string? tags,
            [FromForm] Guid? workingPaperId, [FromForm] Guid? procedureId,
            [FromForm] Guid? supersedesEvidenceId, CancellationToken ct) {
            if (files.Count == 0) {
                return Result<List<Guid>>.Error("No files were provided.");
            }

            var parsedCategory = Enum.TryParse<EvidenceCategory>(category, ignoreCase: true,
                out var value) ? value : EvidenceCategory.Evidence;

            var parsedTags = (tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var uploaded = new List<Guid>();

            foreach (var file in files) {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);

                var result = await _mediator.SendAsync(new UploadEvidenceCommand {
                    EngagementId = engagementId,
                    WorkingPaperId = workingPaperId,
                    ProcedureId = procedureId,
                    SupersedesEvidenceId = files.Count == 1 ? supersedesEvidenceId : null,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Description = description ?? string.Empty,
                    Category = parsedCategory,
                    Tags = parsedTags,
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
        public async Task<Result<IEnumerable<EvidenceRow>>> List(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetEvidenceQuery { EngagementId = engagementId }, ct);

        [HttpGet("{evidenceId:guid}/download-url")]
        public async Task<Result<string>> GetDownloadUrl(Guid engagementId, Guid evidenceId,
            [FromQuery] int? version, CancellationToken ct)
            => await _mediator.SendAsync(new GetEvidenceDownloadUrlQuery {
                EngagementId = engagementId,
                EvidenceId = evidenceId,
                Version = version
            }, ct);
    }
}
