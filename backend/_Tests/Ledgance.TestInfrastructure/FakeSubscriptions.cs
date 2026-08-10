using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.TestInfrastructure {
    public sealed class FakeSubscriptionReader : ISubscriptionReader {
        private readonly Dictionary<(Guid, ProductModule), OrganizationSubscription> _subscriptions = [];

        public FakeSubscriptionReader With(Guid organizationId, ProductModule module,
            PlanCode plan, SubscriptionStatus status = SubscriptionStatus.Active,
            IReadOnlyDictionary<string, string>? overrides = null) {
            _subscriptions[(organizationId, module)] = new OrganizationSubscription(
                module, plan, status, overrides ?? new Dictionary<string, string>());

            return this;
        }

        public Task<OrganizationSubscription> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) =>
            Task.FromResult(_subscriptions.TryGetValue((organizationId, module), out var subscription)
                ? subscription
                : OrganizationSubscription.FreeFor(module));
    }

    public sealed class FakeEntitlementService : IEntitlementService {
        private readonly Dictionary<ProductModule, EntitlementSet> _sets = [];

        public FakeEntitlementService With(ProductModule module, PlanCode plan,
            IReadOnlyDictionary<string, string>? values = null) {
            var resolved = new Dictionary<string, string>(SubscriptionPlanCatalog.For(plan));

            if (values is not null) {
                foreach (var (key, value) in values) {
                    resolved[key] = value;
                }
            }

            _sets[module] = new EntitlementSet(module, plan, resolved);
            return this;
        }

        public Task<EntitlementSet> GetAsync(Guid organizationId, ProductModule module,
            CancellationToken ct) =>
            Task.FromResult(_sets.TryGetValue(module, out var set)
                ? set
                : new EntitlementSet(module, PlanCode.Free,
                    SubscriptionPlanCatalog.For(PlanCode.Free)));
    }
}
