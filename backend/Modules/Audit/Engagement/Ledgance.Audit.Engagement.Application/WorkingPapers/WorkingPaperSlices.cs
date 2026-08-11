using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.WorkingPapers {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class CreateWorkingPaperCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class CreateWorkingPaperCommandValidator : AbstractValidator<CreateWorkingPaperCommand> {
        public CreateWorkingPaperCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Reference).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }

    public class CreateWorkingPaperCommandHandler
        : IRequestHandler<CreateWorkingPaperCommand, Result<Guid>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public CreateWorkingPaperCommandHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _papers = papers;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(CreateWorkingPaperCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var paper = WorkingPaper.Create(request.EngagementId, request.Reference,
                request.Title, request.Content);

            await _papers.AddAsync(paper, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "working_paper.created",
                "WorkingPaper", paper.Id,
                $"created the working paper {paper.Reference} {paper.Title}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(paper.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class UpdateWorkingPaperContentCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public Guid WorkingPaperId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateWorkingPaperContentCommandValidator
        : AbstractValidator<UpdateWorkingPaperContentCommand> {
        public UpdateWorkingPaperContentCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.WorkingPaperId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }

    public class UpdateWorkingPaperContentCommandHandler
        : IRequestHandler<UpdateWorkingPaperContentCommand, Result<bool>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public UpdateWorkingPaperContentCommandHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _papers = papers;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateWorkingPaperContentCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var paper = await _papers.FindAsync(request.WorkingPaperId, ct);

            if (paper is null || paper.EngagementId != request.EngagementId) {
                return Result<bool>.Error("Working paper was not found.");
            }

            paper.UpdateContent(request.Title, request.Content);
            await _papers.UpdateAsync(paper, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "working_paper.updated",
                "WorkingPaper", paper.Id, $"edited the working paper {paper.Reference}.",
                request.EngagementId), ct);

            return Result<bool>.Success(true);
        }
    }

    public enum WorkingPaperSignOffAction { Prepare, Review, Approve }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class SignOffWorkingPaperCommand : ICommand<Result<string>> {
        public Guid EngagementId { get; set; }
        public Guid WorkingPaperId { get; set; }
        public WorkingPaperSignOffAction Action { get; set; }
    }

    public class SignOffWorkingPaperCommandValidator
        : AbstractValidator<SignOffWorkingPaperCommand> {
        public SignOffWorkingPaperCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.WorkingPaperId).NotEmpty();
            RuleFor(x => x.Action).IsInEnum();
        }
    }

    public class SignOffWorkingPaperCommandHandler
        : IRequestHandler<SignOffWorkingPaperCommand, Result<string>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public SignOffWorkingPaperCommandHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _papers = papers;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<string>> HandleAsync(SignOffWorkingPaperCommand request,
            CancellationToken ct) {
            var access = await _access.EnsureMemberAsync(request.EngagementId, ct);
            var userId = _currentUser.Require().UserId;

            var paper = await _papers.FindAsync(request.WorkingPaperId, ct);

            if (paper is null || paper.EngagementId != request.EngagementId) {
                return Result<string>.Error("Working paper was not found.");
            }

            switch (request.Action) {
                case WorkingPaperSignOffAction.Prepare:
                    paper.Prepare(userId);
                    break;
                case WorkingPaperSignOffAction.Review:
                    paper.Review(userId);
                    break;
                case WorkingPaperSignOffAction.Approve:
                    paper.Approve(userId, access.TeamRole
                        ?? throw new DomainRuleException(
                            "Only assigned engagement team members can approve working papers."));
                    break;
            }

            await _papers.UpdateAsync(paper, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "working_paper.signed",
                "WorkingPaper", paper.Id,
                $"marked the working paper {paper.Reference} as {paper.Status}.",
                request.EngagementId), ct);

            return Result<string>.Success(paper.Status.ToString());
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class AddReviewNoteCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public Guid WorkingPaperId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class AddReviewNoteCommandValidator : AbstractValidator<AddReviewNoteCommand> {
        public AddReviewNoteCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.WorkingPaperId).NotEmpty();
            RuleFor(x => x.Text).NotEmpty();
        }
    }

    public class AddReviewNoteCommandHandler
        : IRequestHandler<AddReviewNoteCommand, Result<Guid>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public AddReviewNoteCommandHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _papers = papers;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(AddReviewNoteCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var paper = await _papers.FindAsync(request.WorkingPaperId, ct);

            if (paper is null || paper.EngagementId != request.EngagementId) {
                return Result<Guid>.Error("Working paper was not found.");
            }

            var note = ReviewNote.Raise(_currentUser.Require().UserId, request.Text);
            paper.AddNote(note);
            await _papers.UpdateAsync(paper, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "review_note.raised",
                "WorkingPaper", paper.Id,
                $"raised a review note on the working paper {paper.Reference}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(note.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class ResolveReviewNoteCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public Guid WorkingPaperId { get; set; }
        public Guid NoteId { get; set; }
        public string Resolution { get; set; } = string.Empty;
    }

    public class ResolveReviewNoteCommandValidator : AbstractValidator<ResolveReviewNoteCommand> {
        public ResolveReviewNoteCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.WorkingPaperId).NotEmpty();
            RuleFor(x => x.NoteId).NotEmpty();
            RuleFor(x => x.Resolution).NotEmpty();
        }
    }

    public class ResolveReviewNoteCommandHandler
        : IRequestHandler<ResolveReviewNoteCommand, Result<bool>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ResolveReviewNoteCommandHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _papers = papers;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ResolveReviewNoteCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var paper = await _papers.FindAsync(request.WorkingPaperId, ct);

            if (paper is null || paper.EngagementId != request.EngagementId) {
                return Result<bool>.Error("Working paper was not found.");
            }

            paper.ResolveNote(request.NoteId, _currentUser.Require().UserId, request.Resolution);
            await _papers.UpdateAsync(paper, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "review_note.resolved",
                "WorkingPaper", paper.Id,
                $"resolved a review note on the working paper {paper.Reference}.",
                request.EngagementId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetWorkingPapersQuery : IQuery<Result<IEnumerable<WorkingPaperRow>>> {
        public Guid EngagementId { get; set; }
    }

    public class WorkingPaperRow {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? PreparedBy { get; set; }
        public Guid? ReviewedBy { get; set; }
        public Guid? ApprovedBy { get; set; }
        public int OpenNotes { get; set; }
    }

    public class GetWorkingPapersQueryHandler
        : IRequestHandler<GetWorkingPapersQuery, Result<IEnumerable<WorkingPaperRow>>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;

        public GetWorkingPapersQueryHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access) {
            _papers = papers;
            _access = access;
        }

        public async Task<Result<IEnumerable<WorkingPaperRow>>> HandleAsync(
            GetWorkingPapersQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var papers = await _papers.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<WorkingPaperRow>>.Success(papers
                .Select(paper => new WorkingPaperRow {
                    Id = paper.Id,
                    Reference = paper.Reference,
                    Title = paper.Title,
                    Status = paper.Status.ToString(),
                    PreparedBy = paper.PreparedBy,
                    ReviewedBy = paper.ReviewedBy,
                    ApprovedBy = paper.ApprovedBy,
                    OpenNotes = paper.OpenNoteCount
                }));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetWorkingPaperByIdQuery : IQuery<Result<WorkingPaperDetail>> {
        public Guid EngagementId { get; set; }
        public Guid WorkingPaperId { get; set; }
    }

    public class WorkingPaperDetail : WorkingPaperRow {
        public string Content { get; set; } = string.Empty;
        public List<ReviewNoteRow> Notes { get; set; } = [];
    }

    public class ReviewNoteRow {
        public Guid Id { get; set; }
        public Guid AuthorUserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }
        public string? Resolution { get; set; }
    }

    public class GetWorkingPaperByIdQueryHandler
        : IRequestHandler<GetWorkingPaperByIdQuery, Result<WorkingPaperDetail>> {
        private readonly IWorkingPaperRepository _papers;
        private readonly IEngagementAccessGuard _access;

        public GetWorkingPaperByIdQueryHandler(IWorkingPaperRepository papers,
            IEngagementAccessGuard access) {
            _papers = papers;
            _access = access;
        }

        public async Task<Result<WorkingPaperDetail>> HandleAsync(
            GetWorkingPaperByIdQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var paper = await _papers.FindAsync(request.WorkingPaperId, ct);

            if (paper is null || paper.EngagementId != request.EngagementId) {
                return Result<WorkingPaperDetail>.Error("Working paper was not found.");
            }

            return Result<WorkingPaperDetail>.Success(new WorkingPaperDetail {
                Id = paper.Id,
                Reference = paper.Reference,
                Title = paper.Title,
                Status = paper.Status.ToString(),
                Content = paper.Content,
                PreparedBy = paper.PreparedBy,
                ReviewedBy = paper.ReviewedBy,
                ApprovedBy = paper.ApprovedBy,
                OpenNotes = paper.OpenNoteCount,
                Notes = paper.Notes.Select(note => new ReviewNoteRow {
                    Id = note.Id,
                    AuthorUserId = note.AuthorUserId,
                    Text = note.Text,
                    CreatedAt = note.CreatedAt,
                    IsResolved = note.IsResolved,
                    Resolution = note.Resolution
                }).ToList()
            });
        }
    }
}
