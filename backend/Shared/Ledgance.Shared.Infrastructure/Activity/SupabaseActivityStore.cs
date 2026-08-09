using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Infrastructure.Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Activity {
    [Table("activity_log")]
    public class ActivityLogModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("module")]
        public string Module { get; set; } = string.Empty;

        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("subject_type")]
        public string SubjectType { get; set; } = string.Empty;

        [Column("subject_id")]
        public Guid SubjectId { get; set; }

        [Column("summary")]
        public string Summary { get; set; } = string.Empty;

        [Column("engagement_id")]
        public Guid? EngagementId { get; set; }

        [Column("actor_user_id")]
        public Guid ActorUserId { get; set; }

        [Column("actor_email")]
        public string ActorEmail { get; set; } = string.Empty;

        [Column("occurred_at")]
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Append-only. Activity rows are never updated or deleted — the trail is the evidence that
    /// something happened, in order.
    /// </summary>
    internal sealed class SupabaseActivityStore : IActivityRecorder, IActivityReader {
        private readonly SupabaseRepository<ActivityLogModel> _repository;
        private readonly ICurrentUserAccessor _currentUser;

        public SupabaseActivityStore(SupabaseRepository<ActivityLogModel> repository,
            ICurrentUserAccessor currentUser) {
            _repository = repository;
            _currentUser = currentUser;
        }

        public async Task RecordAsync(ActivityEntry entry, CancellationToken ct) {
            var user = _currentUser.Require();

            await _repository.InsertAsync(new ActivityLogModel {
                Id = Guid.NewGuid(),
                Module = entry.Module,
                Action = entry.Action,
                SubjectType = entry.SubjectType,
                SubjectId = entry.SubjectId,
                Summary = entry.Summary,
                EngagementId = entry.EngagementId,
                ActorUserId = user.UserId,
                ActorEmail = user.Email,
                OccurredAt = DateTime.UtcNow
            }, ct);
        }

        public async Task<IReadOnlyList<RecordedActivity>> ListAsync(Guid? engagementId,
            int limit, CancellationToken ct) {
            var query = _repository.Query();

            if (engagementId is not null) {
                query = query.Filter("engagement_id", Constants.Operator.Equals,
                    engagementId.Value.ToString());
            }

            var rows = await query
                .Order("occurred_at", Constants.Ordering.Descending)
                .Limit(limit)
                .Get(ct);

            return rows.Models
                .Select(row => new RecordedActivity(row.Id, row.Module, row.Action,
                    row.SubjectType, row.SubjectId, row.Summary, row.EngagementId,
                    row.ActorUserId, row.ActorEmail, row.OccurredAt))
                .ToList();
        }
    }
}
