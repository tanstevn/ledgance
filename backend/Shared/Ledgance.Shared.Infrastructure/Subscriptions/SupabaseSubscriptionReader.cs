using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Supabase.Models;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Subscriptions {
    internal sealed class SupabaseSubscriptionReader : ISubscriptionReader {
        private readonly Client _client;

        public SupabaseSubscriptionReader(Client client) {
            _client = client;
        }

        public async Task<OrganizationSubscription> GetAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) {
            var rows = await _client.From<OrganizationSubscriptionModel>()
                .Filter("organization_id", Constants.Operator.Equals, organizationId.ToString())
                .Filter("module", Constants.Operator.Equals, module.ToString())
                .Limit(1)
                .Get(ct);

            var subscription = rows.Models.FirstOrDefault();

            if (subscription is null) {
                return OrganizationSubscription.FreeFor(module);
            }

            return new OrganizationSubscription(
                module,
                Parse(subscription.Plan, PlanCode.Free),
                Parse(subscription.Status, SubscriptionStatus.Canceled),
                subscription.EntitlementOverrides);
        }

        private static TEnum Parse<TEnum>(string value, TEnum fallback)
            where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
    }
}
