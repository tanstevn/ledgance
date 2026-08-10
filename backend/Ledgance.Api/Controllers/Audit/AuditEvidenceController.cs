using Ledgance.Audit.Engagement.Application.Evidence;
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

        [HttpPost]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<Result<Guid>> Upload(Guid engagementId, [FromForm] IFormFile file,
            [FromForm] string? description, [FromForm] Guid? workingPaperId,
            [FromForm] Guid? procedureId, [FromForm] Guid? supersedesEvidenceId,
            CancellationToken ct) {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);

            return await _mediator.SendAsync(new UploadEvidenceCommand {
                EngagementId = engagementId,
                WorkingPaperId = workingPaperId,
                ProcedureId = procedureId,
                SupersedesEvidenceId = supersedesEvidenceId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Description = description ?? string.Empty,
                Content = stream.ToArray()
            }, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<EvidenceRow>>> List(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetEvidenceQuery { EngagementId = engagementId }, ct);

        [HttpGet("{evidenceId:guid}/download-url")]
        public async Task<Result<string>> GetDownloadUrl(Guid engagementId, Guid evidenceId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetEvidenceDownloadUrlQuery {
                EngagementId = engagementId,
                EvidenceId = evidenceId
            }, ct);
    }
}
