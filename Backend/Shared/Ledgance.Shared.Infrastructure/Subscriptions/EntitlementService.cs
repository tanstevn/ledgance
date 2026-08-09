using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Options;

namespace Ledgance.Shared.Infrastructure.Subscriptions {
    public sealed class SubscriptionSettings {
        public const string SectionName = "Subscriptions";

        /// <summary>
        /// Optional per-plan entitlement overrides, keyed by <see cref="PlanCode"/> name.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Plans { get; set; } = [];
    }

    public sealed class EntitlementService : IEntitlementService {
        private readonly Dictionary<(Guid, ProductModule), EntitlementSet> _resolved = [];
        private readonly ISubscriptionReader _subscriptions;
        private readonly SubscriptionSettings _settings;

        public EntitlementService(ISubscriptionReader subscriptions,
            IOptions<SubscriptionSettings> settings) {
            _subscriptions = subscriptions;
            _settings = settings.Value;
        }

        public async Task<EntitlementSet> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) {
            if (_resolved.TryGetValue((organizationId, module), out var cached)) {
                return cached;
            }

            var subscription = await _subscriptions.GetAsync(organizationId, module, ct);

            var plan = subscription.GrantsPaidPlan
                ? subscription.Plan
                : PlanCode.Free;

            var values = new Dictionary<string, string>(SubscriptionPlanCatalog.For(plan));

            if (_settings.Plans.TryGetValue(plan.ToString(), out var configured)) {
                Apply(values, configured);
            }

            Apply(values, subscription.Overrides);

            var entitlements = new EntitlementSet(module, plan, values);
            _resolved[(organizationId, module)] = entitlements;

            return entitlements;
        }

        private static void Apply(Dictionary<string, string> target,
            IReadOnlyDictionary<string, string> overrides) {
            foreach (var (key, value) in overrides) {
                target[key] = value;
            }
        }
    }
}
