using Ledgance.Audit.Engagement.Application.WorkingPapers;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers.Audit {
    [Route("api/audit/engagements/{engagementId:guid}/working-papers")]
    [ApiController]
    public class AuditWorkingPaperController : ControllerBase {
        private readonly IMediator _mediator;

        public AuditWorkingPaperController(IMediator mediator) {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<Result<Guid>> Create(Guid engagementId,
            [FromBody] CreateWorkingPaperCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpGet]
        public async Task<Result<IEnumerable<WorkingPaperRow>>> List(Guid engagementId,
            CancellationToken ct)
            => await _mediator.SendAsync(
                new GetWorkingPapersQuery { EngagementId = engagementId }, ct);

        [HttpGet("{paperId:guid}")]
        public async Task<Result<WorkingPaperDetail>> GetById(Guid engagementId, Guid paperId,
            CancellationToken ct)
            => await _mediator.SendAsync(new GetWorkingPaperByIdQuery {
                EngagementId = engagementId,
                WorkingPaperId = paperId
            }, ct);

        [HttpPut("{paperId:guid}")]
        public async Task<Result<bool>> UpdateContent(Guid engagementId, Guid paperId,
            [FromBody] UpdateWorkingPaperContentCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.WorkingPaperId = paperId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{paperId:guid}/sign-off")]
        public async Task<Result<string>> SignOff(Guid engagementId, Guid paperId,
            [FromBody] SignOffWorkingPaperCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.WorkingPaperId = paperId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{paperId:guid}/notes")]
        public async Task<Result<Guid>> AddNote(Guid engagementId, Guid paperId,
            [FromBody] AddReviewNoteCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.WorkingPaperId = paperId;
            return await _mediator.SendAsync(command, ct);
        }

        [HttpPost("{paperId:guid}/notes/{noteId:guid}/resolve")]
        public async Task<Result<bool>> ResolveNote(Guid engagementId, Guid paperId,
            Guid noteId, [FromBody] ResolveReviewNoteCommand command, CancellationToken ct) {
            command.EngagementId = engagementId;
            command.WorkingPaperId = paperId;
            command.NoteId = noteId;
            return await _mediator.SendAsync(command, ct);
        }
    }
}
