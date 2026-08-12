using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Shared.Application.Subscriptions {
    public sealed class EntitlementSet {
        public const long Unlimited = -1;

        private readonly IReadOnlyDictionary<string, string> _values;

        public EntitlementSet(ProductModule module, PlanCode plan,
            IReadOnlyDictionary<string, string> values) {
            Module = module;
            Plan = plan;
            _values = values;
        }

        public ProductModule Module { get; }
        public PlanCode Plan { get; }

        public bool Has(string capability) =>
            _values.TryGetValue(capability, out var value)
            && bool.TryParse(value, out var flag)
            && flag;

        public long Limit(string key) =>
            _values.TryGetValue(key, out var value) && long.TryParse(value, out var limit)
                ? limit
                : 0;

        /// <summary>
        /// A non-numeric entitlement value. The fallback is the caller's own floor, so a plan
        /// missing the key lands on the least capable level of that ladder rather than on
        /// another ladder's vocabulary.
        /// </summary>
        public string Value(string key, string fallback) =>
            _values.TryGetValue(key, out var value) ? value : fallback;

        public string Tier(string key) =>
            Value(key, AiTiers.Basic);

        public bool IsWithinLimit(string key, long requestedTotal) {
            var limit = Limit(key);
            return limit == Unlimited || requestedTotal <= limit;
        }

        public void RequireCapability(string capability) {
            if (!Has(capability)) {
                throw EntitlementException.NotIncluded(capability);
            }
        }

        public void RequireWithinLimit(string key, long requestedTotal) {
            if (!IsWithinLimit(key, requestedTotal)) {
                throw EntitlementException.LimitReached(key, Limit(key));
            }
        }
    }
}
