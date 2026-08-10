namespace Ledgance.Shared.Application.Subscriptions {
    public enum SubscriptionStatus {
        Active,
        Trialing,
        PastDue,
        Canceled
    }

    public sealed record OrganizationSubscription(
        ProductModule Module,
        PlanCode Plan,
        SubscriptionStatus Status,
        IReadOnlyDictionary<string, string> Overrides) {
        public bool GrantsPaidPlan =>
            Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing;

        public static OrganizationSubscription FreeFor(ProductModule module) =>
            new(module, PlanCode.Free, SubscriptionStatus.Active,
                new Dictionary<string, string>());
    }

    public interface ISubscriptionReader {
        Task<OrganizationSubscription> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct);
    }

    public interface IEntitlementService {
        Task<EntitlementSet> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct);
    }
}
