namespace Ledgance.Shared.Application.Activity {
    /// <summary>
    /// <paramref name="ContextId"/> scopes an entry to its product's unit of work — the
    /// engagement in Audit, the accounting entity in Accounting.
    /// </summary>
    public sealed record ActivityEntry(
        string Module,
        string Action,
        string SubjectType,
        Guid SubjectId,
        string Summary,
        Guid? ContextId = null);

    public sealed record RecordedActivity(
        Guid Id,
        string Module,
        string Action,
        string SubjectType,
        Guid SubjectId,
        string Summary,
        Guid? ContextId,
        Guid ActorUserId,
        string ActorEmail,
        DateTime OccurredAt);

    public interface IActivityRecorder {
        Task RecordAsync(ActivityEntry entry, CancellationToken ct);
    }

    public interface IActivityReader {
        Task<IReadOnlyList<RecordedActivity>> ListAsync(Guid? contextId,
            int limit, CancellationToken ct);
    }
}
