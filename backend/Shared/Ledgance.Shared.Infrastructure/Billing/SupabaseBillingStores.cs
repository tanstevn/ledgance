using Ledgance.Shared.Application.Billing;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Supabase.Models;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Billing {
    /// <summary>
    /// Reads and writes subscription rows for the billing path. This deliberately bypasses
    /// <c>SupabaseRepository</c>: webhooks arrive with no user and therefore no organization
    /// context, so tenancy cannot come from the caller. Every query below instead filters on an
    /// organization id the application itself resolved — from checkout metadata the provider
    /// signed, or from the stored subscription/customer identifier — and never from request
    /// input.
    /// </summary>
    internal sealed class SupabaseSubscriptionStore : ISubscriptionStore {
        private readonly Client _client;

        public SupabaseSubscriptionStore(Client client) {
            _client = client;
        }

        public async Task<StoredSubscription?> FindAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) {
            var rows = await _client.From<OrganizationSubscriptionModel>()
                .Filter("organization_id", Constants.Operator.Equals, organizationId.ToString())
                .Filter("module", Constants.Operator.Equals, module.ToString())
                .Limit(1)
                .Get(ct);

            return ToDomain(rows.Models.FirstOrDefault());
        }

        public async Task<StoredSubscription?> FindBySubscriptionIdAsync(string subscriptionId,
            CancellationToken ct) {
            var rows = await _client.From<OrganizationSubscriptionModel>()
                .Filter("stripe_subscription_id", Constants.Operator.Equals, subscriptionId)
                .Limit(1)
                .Get(ct);

            return ToDomain(rows.Models.FirstOrDefault());
        }

        public async Task<StoredSubscription?> FindByCustomerIdAsync(string customerId,
            CancellationToken ct) {
            var rows = await _client.From<OrganizationSubscriptionModel>()
                .Filter("stripe_customer_id", Constants.Operator.Equals, customerId)
                .Limit(1)
                .Get(ct);

            return ToDomain(rows.Models.FirstOrDefault());
        }

        public async Task UpsertAsync(StoredSubscription subscription, CancellationToken ct) {
            var rows = await _client.From<OrganizationSubscriptionModel>()
                .Filter("organization_id", Constants.Operator.Equals,
                    subscription.OrganizationId.ToString())
                .Filter("module", Constants.Operator.Equals, subscription.Module.ToString())
                .Limit(1)
                .Get(ct);

            var model = rows.Models.FirstOrDefault() ?? new OrganizationSubscriptionModel {
                Id = Guid.NewGuid(),
                OrganizationId = subscription.OrganizationId,
                Module = subscription.Module.ToString()
            };

            model.Plan = subscription.Plan.ToString();
            model.Status = subscription.Status.ToString();
            model.StripeCustomerId = subscription.CustomerId;
            model.StripeSubscriptionId = subscription.SubscriptionId;
            model.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
            model.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
            model.LastEventAt = subscription.LastEventAt;

            if (rows.Models.Count == 0) {
                await _client.From<OrganizationSubscriptionModel>()
                    .Insert(model, cancellationToken: ct);
                return;
            }

            await _client.From<OrganizationSubscriptionModel>()
                .Filter("id", Constants.Operator.Equals, model.Id.ToString())
                .Set(row => row.Plan!, model.Plan)
                .Set(row => row.Status!, model.Status)
                .Set(row => row.StripeCustomerId!, model.StripeCustomerId!)
                .Set(row => row.StripeSubscriptionId!, model.StripeSubscriptionId!)
                .Set(row => row.CurrentPeriodEnd!, model.CurrentPeriodEnd)
                .Set(row => row.CancelAtPeriodEnd, model.CancelAtPeriodEnd)
                .Set(row => row.LastEventAt!, model.LastEventAt)
                .Update(cancellationToken: ct);
        }

        private static StoredSubscription? ToDomain(OrganizationSubscriptionModel? model) =>
            model is null
                ? null
                : new StoredSubscription(
                    model.OrganizationId,
                    Enum.TryParse<ProductModule>(model.Module, out var module)
                        ? module
                        : ProductModule.Audit,
                    Enum.TryParse<PlanCode>(model.Plan, out var plan) ? plan : PlanCode.Free,
                    Enum.TryParse<SubscriptionStatus>(model.Status, out var status)
                        ? status
                        : SubscriptionStatus.Canceled,
                    model.StripeCustomerId,
                    model.StripeSubscriptionId,
                    model.CurrentPeriodEnd,
                    model.CancelAtPeriodEnd,
                    model.LastEventAt);
    }

    /// <summary>
    /// Idempotency for webhook delivery: the unique index on the event id makes a repeated
    /// delivery fail the insert, which is what "already applied" means here.
    /// </summary>
    internal sealed class SupabaseProcessedEventStore : IProcessedEventStore {
        private readonly Client _client;

        public SupabaseProcessedEventStore(Client client) {
            _client = client;
        }

        public async Task<bool> TryRecordAsync(string eventId, string eventType,
            CancellationToken ct) {
            var existing = await _client.From<BillingEventModel>()
                .Filter("event_id", Constants.Operator.Equals, eventId)
                .Limit(1)
                .Get(ct);

            if (existing.Models.Count > 0) {
                return false;
            }

            try {
                await _client.From<BillingEventModel>().Insert(new BillingEventModel {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    EventType = eventType,
                    ReceivedAt = DateTime.UtcNow
                }, cancellationToken: ct);

                return true;
            }
            catch (global::Supabase.Postgrest.Exceptions.PostgrestException) {
                return false;
            }
        }
    }
}
