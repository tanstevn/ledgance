using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum WorkingPaperStatus { Draft, Prepared, Reviewed, Approved }

    public sealed record ReviewNote(
        Guid Id,
        Guid AuthorUserId,
        string Text,
        DateTime CreatedAt,
        Guid? ResolvedBy,
        DateTime? ResolvedAt,
        string? Resolution) {
        public bool IsResolved => ResolvedAt is not null;

        public static ReviewNote Raise(Guid authorUserId, string text) {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            return new ReviewNote(Guid.NewGuid(), authorUserId, text.Trim(),
                DateTime.UtcNow, null, null, null);
        }

        public ReviewNote Resolve(Guid resolverUserId, string resolution) {
            if (IsResolved) {
                throw new DomainRuleException("This review note is already resolved.");
            }

            if (string.IsNullOrWhiteSpace(resolution)) {
                throw new DomainRuleException("Resolving a review note requires a resolution.");
            }

            return this with {
                ResolvedBy = resolverUserId,
                ResolvedAt = DateTime.UtcNow,
                Resolution = resolution.Trim()
            };
        }
    }

    /// <summary>
    /// Preparer/reviewer segregation: the person who prepared a paper can never review or
    /// approve it, and any content change withdraws sign-offs already given.
    /// </summary>
    public sealed class WorkingPaper {
        private WorkingPaper() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public string Reference { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public WorkingPaperStatus Status { get; private set; }
        public Guid? PreparedBy { get; private set; }
        public DateTime? PreparedAt { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public Guid? ApprovedBy { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public List<ReviewNote> Notes { get; private set; } = [];

        public int OpenNoteCount => Notes.Count(note => !note.IsResolved);

        public static WorkingPaper Create(Guid engagementId, string reference, string title,
            string content) {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            return new WorkingPaper {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Reference = reference.Trim(),
                Title = title.Trim(),
                Content = content,
                Status = WorkingPaperStatus.Draft
            };
        }

        public static WorkingPaper Restore(Guid id, Guid engagementId, string reference,
            string title, string content, WorkingPaperStatus status, Guid? preparedBy,
            DateTime? preparedAt, Guid? reviewedBy, DateTime? reviewedAt, Guid? approvedBy,
            DateTime? approvedAt, List<ReviewNote> notes) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Reference = reference,
                Title = title,
                Content = content,
                Status = status,
                PreparedBy = preparedBy,
                PreparedAt = preparedAt,
                ReviewedBy = reviewedBy,
                ReviewedAt = reviewedAt,
                ApprovedBy = approvedBy,
                ApprovedAt = approvedAt,
                Notes = notes
            };

        public void UpdateContent(string title, string content) {
            if (Status == WorkingPaperStatus.Approved) {
                throw new DomainRuleException(
                    "An approved working paper can no longer be edited.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            Title = title.Trim();
            Content = content;

            Status = WorkingPaperStatus.Draft;
            PreparedBy = null;
            PreparedAt = null;
            ReviewedBy = null;
            ReviewedAt = null;
        }

        public void Prepare(Guid userId) {
            if (Status != WorkingPaperStatus.Draft) {
                throw new DomainRuleException("Only a draft working paper can be prepared.");
            }

            Status = WorkingPaperStatus.Prepared;
            PreparedBy = userId;
            PreparedAt = DateTime.UtcNow;
        }

        public void Review(Guid userId) {
            if (Status != WorkingPaperStatus.Prepared) {
                throw new DomainRuleException("Only a prepared working paper can be reviewed.");
            }

            if (userId == PreparedBy) {
                throw new DomainRuleException(
                    "A working paper cannot be reviewed by its preparer.");
            }

            Status = WorkingPaperStatus.Reviewed;
            ReviewedBy = userId;
            ReviewedAt = DateTime.UtcNow;
        }

        public void Approve(Guid userId, EngagementRole actorTeamRole) {
            if (Status != WorkingPaperStatus.Reviewed) {
                throw new DomainRuleException("Only a reviewed working paper can be approved.");
            }

            if (userId == PreparedBy) {
                throw new DomainRuleException(
                    "A working paper cannot be approved by its preparer.");
            }

            if (actorTeamRole is not (EngagementRole.Manager or EngagementRole.Partner)) {
                throw new DomainRuleException(
                    "Only an engagement manager or partner can approve a working paper.");
            }

            if (OpenNoteCount > 0) {
                throw new DomainRuleException(
                    $"{OpenNoteCount} review note(s) must be resolved before approval.");
            }

            Status = WorkingPaperStatus.Approved;
            ApprovedBy = userId;
            ApprovedAt = DateTime.UtcNow;
        }

        public void AddNote(ReviewNote note) {
            if (Status == WorkingPaperStatus.Approved) {
                throw new DomainRuleException(
                    "Review notes cannot be added to an approved working paper.");
            }

            Notes.Add(note);
        }

        public void ResolveNote(Guid noteId, Guid resolverUserId, string resolution) {
            var index = Notes.FindIndex(note => note.Id == noteId);

            if (index < 0) {
                throw new DomainRuleException("The review note was not found.");
            }

            Notes[index] = Notes[index].Resolve(resolverUserId, resolution);
        }
    }
}
