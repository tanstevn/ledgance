using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Application.Ai {
    /// <summary>
    /// Who is being charged for a unit of AI work and what for. Client and engagement are
    /// recorded when the operation has one, so usage can be read back per engagement rather
    /// than only as an organization total.
    /// </summary>
    public sealed record AiUsageContext(
        Guid OrganizationId,
        Guid UserId,
        ProductModule Module,
        string Capability,
        Guid? ClientId = null,
        Guid? EngagementId = null);

    /// <summary>
    /// The window usage accumulates in. It follows the organization's paid subscription period
    /// where there is one, so an allowance refills when the customer is billed rather than on an
    /// unrelated calendar boundary.
    /// </summary>
    public sealed record AiUsagePeriod(string Key, DateTime? ResetsAt);

    public sealed record AiUsageSnapshot(
        string PeriodKey,
        DateTime? ResetsAt,
        long Used,
        long Limit) {
        public bool IsUnlimited => Limit == EntitlementSet.Unlimited;

        public long Remaining => IsUnlimited
            ? long.MaxValue
            : Math.Max(0, Limit - Used);

        /// <summary>
        /// True once four fifths of the allowance is gone — early enough for a surface to warn
        /// before work starts failing.
        /// </summary>
        public bool IsApproachingLimit =>
            !IsUnlimited && Limit > 0 && Used * 5 >= Limit * 4;
    }

    /// <summary>
    /// Usage taken before the work runs. Holding a reservation is what makes concurrent requests
    /// safe: the allowance is decremented atomically up front, so two callers cannot both spend
    /// the same remaining credits. A run that never reached the provider gives it back.
    /// </summary>
    public sealed record AiUsageReservation(
        Guid Id,
        string PeriodKey,
        long Units,
        long UsedAfter,
        long Limit) {
        public AiUsageSnapshot Snapshot(DateTime? resetsAt) =>
            new(PeriodKey, resetsAt, UsedAfter, Limit);
    }

    /// <summary>
    /// Product-level AI consumption accounting, per organization and product module. Units are a
    /// product currency, not a provider one — swapping a model or provider must not change what
    /// an operation costs a customer.
    /// </summary>
    public interface IAiUsageMeter {
        Task<AiUsageSnapshot> GetAsync(Guid organizationId, ProductModule module,
            AiUsagePeriod period, long limit, CancellationToken ct);

        /// <summary>
        /// Atomically takes <paramref name="units"/> from the allowance, or returns null when
        /// that would exceed <paramref name="limit"/>. A limit of
        /// <see cref="EntitlementSet.Unlimited"/> always succeeds and is still recorded.
        /// </summary>
        Task<AiUsageReservation?> TryReserveAsync(AiUsageContext context, AiUsagePeriod period,
            long units, long limit, CancellationToken ct);

        Task ReleaseAsync(Guid organizationId, AiUsageReservation reservation,
            CancellationToken ct);
    }

    public interface IAiUsagePeriodResolver {
        Task<AiUsagePeriod> ResolveAsync(Guid organizationId, ProductModule module,
            CancellationToken ct);
    }

    /// <summary>
    /// What each AI capability costs in units. Declared once per module in that module's
    /// capability catalogue and overridable through configuration, so retuning a price is a
    /// settings change rather than a code change.
    /// </summary>
    public interface IAiOperationCosts {
        long CostOf(string capability, long declaredCost);
    }

    public static class AiUsage {
        public static string CalendarPeriod() =>
            DateTime.UtcNow.ToString("yyyy-MM");

        /// <summary>
        /// Rough token estimate (~4 characters per token) used for the context-size entitlement
        /// gate. Exact counts are provider-specific and not needed for limit enforcement.
        /// </summary>
        public static int EstimateTokens(string text) =>
            (text.Length + 3) / 4;
    }
}
