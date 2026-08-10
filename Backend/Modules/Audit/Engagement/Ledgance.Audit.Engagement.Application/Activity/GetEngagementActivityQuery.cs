using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Activity {
    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetEngagementActivityQuery : IQuery<Result<IEnumerable<ActivityRow>>> {
        public Guid EngagementId { get; set; }
        public int Limit { get; set; } = 50;
    }

    public class ActivityRow {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string SubjectType { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public Guid ActorUserId { get; set; }
        public string ActorEmail { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }

    public class GetEngagementActivityQueryValidator
        : AbstractValidator<GetEngagementActivityQuery> {
        public GetEngagementActivityQueryValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Limit).InclusiveBetween(1, 200);
        }
    }

    public class GetEngagementActivityQueryHandler
        : IRequestHandler<GetEngagementActivityQuery, Result<IEnumerable<ActivityRow>>> {
        private readonly IActivityReader _activity;
        private readonly IEngagementAccessGuard _access;

        public GetEngagementActivityQueryHandler(IActivityReader activity,
            IEngagementAccessGuard access) {
            _activity = activity;
            _access = access;
        }

        public async Task<Result<IEnumerable<ActivityRow>>> HandleAsync(
            GetEngagementActivityQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var entries = await _activity.ListAsync(request.EngagementId, request.Limit, ct);

            return Result<IEnumerable<ActivityRow>>.Success(entries
                .Select(entry => new ActivityRow {
                    Id = entry.Id,
                    Action = entry.Action,
                    SubjectType = entry.SubjectType,
                    SubjectId = entry.SubjectId,
                    Summary = entry.Summary,
                    ActorUserId = entry.ActorUserId,
                    ActorEmail = entry.ActorEmail,
                    OccurredAt = entry.OccurredAt
                }));
        }
    }
}
