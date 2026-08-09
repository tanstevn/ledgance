namespace Ledgance.Shared.Application.Activity {
    public sealed record ActivityEntry(
        string Module,
        string Action,
        string SubjectType,
        Guid SubjectId,
        string Summary,
        Guid? EngagementId = null);

    public sealed record RecordedActivity(
        Guid Id,
        string Module,
        string Action,
        string SubjectType,
        Guid SubjectId,
        string Summary,
        Guid? EngagementId,
        Guid ActorUserId,
        string ActorEmail,
        DateTime OccurredAt);

    public interface IActivityRecorder {
        Task RecordAsync(ActivityEntry entry, CancellationToken ct);
    }

    public interface IActivityReader {
        Task<IReadOnlyList<RecordedActivity>> ListAsync(Guid? engagementId,
            int limit, CancellationToken ct);
    }
}
