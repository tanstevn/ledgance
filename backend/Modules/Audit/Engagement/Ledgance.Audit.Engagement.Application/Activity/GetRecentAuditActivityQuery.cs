using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Activity {
    /// <summary>
    /// The organization's recent Audit activity, confined to the caller's engagements: team
    /// membership decides visibility (ADR-017), so the feed never leaks another team's work.
    /// </summary>
    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetRecentAuditActivityQuery : IQuery<Result<IEnumerable<ActivityRow>>> {
        public int Limit { get; set; } = 10;
    }

    public class GetRecentAuditActivityQueryValidator
        : AbstractValidator<GetRecentAuditActivityQuery> {
        public GetRecentAuditActivityQueryValidator() {
            RuleFor(x => x.Limit).InclusiveBetween(1, 50);
        }
    }

    public class GetRecentAuditActivityQueryHandler
        : IRequestHandler<GetRecentAuditActivityQuery, Result<IEnumerable<ActivityRow>>> {
        private readonly IActivityReader _activity;
        private readonly ITeamRepository _team;
        private readonly ICurrentUserAccessor _currentUser;

        public GetRecentAuditActivityQueryHandler(IActivityReader activity,
            ITeamRepository team, ICurrentUserAccessor currentUser) {
            _activity = activity;
            _team = team;
            _currentUser = currentUser;
        }

        public async Task<Result<IEnumerable<ActivityRow>>> HandleAsync(
            GetRecentAuditActivityQuery request, CancellationToken ct) {
            var memberOf = (await _team.ListEngagementIdsForUserAsync(
                _currentUser.Require().UserId, ct)).ToHashSet();

            // Over-fetch before confinement so a busy foreign engagement cannot starve the
            // caller's own feed out of the window.
            var entries = await _activity.ListRecentAsync("Audit", request.Limit * 5, ct);

            return Result<IEnumerable<ActivityRow>>.Success(entries
                .Where(entry => entry.ContextId is null
                    || memberOf.Contains(entry.ContextId.Value))
                .Take(request.Limit)
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
