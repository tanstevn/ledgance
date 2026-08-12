using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Supabase;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Ai {
    [Table("ai_usage")]
    public class AiUsageModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("module")]
        public string Module { get; set; } = string.Empty;

        [Column("period")]
        public string Period { get; set; } = string.Empty;

        [Column("units_used")]
        public long UnitsUsed { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Spending and releasing both go through database functions rather than a read-then-write
    /// from the application: the check and the decrement have to happen under one row lock, or
    /// two simultaneous requests will both see the same remaining allowance and both proceed.
    /// Reads use the ordinary tenant-scoped repository.
    /// </summary>
    internal sealed class SupabaseAiUsageMeter : IAiUsageMeter {
        private const string ConsumeFunction = "consume_ai_units";
        private const string ReleaseFunction = "release_ai_units";

        private readonly SupabaseRepository<AiUsageModel> _repository;
        private readonly Client _client;

        public SupabaseAiUsageMeter(SupabaseRepository<AiUsageModel> repository, Client client) {
            _repository = repository;
            _client = client;
        }

        public async Task<AiUsageSnapshot> GetAsync(Guid organizationId, ProductModule module,
            AiUsagePeriod period, long limit, CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("module", Constants.Operator.Equals, module.ToString())
                .Filter("period", Constants.Operator.Equals, period.Key)
                .Limit(1)
                .Get(ct);

            return new AiUsageSnapshot(period.Key, period.ResetsAt,
                rows.Models.FirstOrDefault()?.UnitsUsed ?? 0, limit);
        }

        public async Task<AiUsageReservation?> TryReserveAsync(AiUsageContext context,
            AiUsagePeriod period, long units, long limit, CancellationToken ct) {
            var response = await _client.Rpc(ConsumeFunction, new Dictionary<string, object?> {
                ["p_organization_id"] = context.OrganizationId,
                ["p_module"] = context.Module.ToString(),
                ["p_period"] = period.Key,
                ["p_units"] = units,
                ["p_limit"] = limit,
                ["p_user_id"] = context.UserId,
                ["p_capability"] = context.Capability,
                ["p_client_id"] = context.ClientId,
                ["p_engagement_id"] = context.EngagementId
            });

            var granted = JsonConvert.DeserializeObject<List<ConsumeResult>>(
                response.Content ?? "[]")?.FirstOrDefault();

            return granted is null
                ? null
                : new AiUsageReservation(granted.EventId, period.Key, units,
                    granted.UnitsUsed, limit);
        }

        public async Task ReleaseAsync(Guid organizationId, AiUsageReservation reservation,
            CancellationToken ct) =>
            await _client.Rpc(ReleaseFunction, new Dictionary<string, object?> {
                ["p_organization_id"] = organizationId,
                ["p_event_id"] = reservation.Id
            });

        private sealed class ConsumeResult {
            [JsonProperty("event_id")] public Guid EventId { get; set; }
            [JsonProperty("total_units")] public long UnitsUsed { get; set; }
        }
    }

    /// <summary>
    /// Usage accumulates against the organization's paid billing period where it has one, so an
    /// allowance refills when the customer is charged. Without a live subscription there is no
    /// billing period to follow and the calendar month applies, which is what a Free
    /// organization gets.
    /// </summary>
    internal sealed class SubscriptionAiUsagePeriodResolver : IAiUsagePeriodResolver {
        private readonly Ledgance.Shared.Application.Billing.ISubscriptionStore _subscriptions;

        public SubscriptionAiUsagePeriodResolver(
            Ledgance.Shared.Application.Billing.ISubscriptionStore subscriptions) {
            _subscriptions = subscriptions;
        }

        public async Task<AiUsagePeriod> ResolveAsync(Guid organizationId, ProductModule module,
            CancellationToken ct) {
            var subscription = await _subscriptions.FindAsync(organizationId, module, ct);

            if (subscription is { CurrentPeriodEnd: { } end }
                && subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing
                && end > DateTime.UtcNow) {
                return new AiUsagePeriod($"sub:{end:yyyy-MM-dd}", end);
            }

            var now = DateTime.UtcNow;

            return new AiUsagePeriod(AiUsage.CalendarPeriod(),
                new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1));
        }
    }

    /// <summary>
    /// The cost of an AI operation, defaulting to what the module's capability catalogue
    /// declares and overridable per capability through <c>Ai:OperationCosts</c>. A configured
    /// value below one is ignored — a free operation would make the allowance meaningless.
    /// </summary>
    internal sealed class ConfiguredAiOperationCosts : IAiOperationCosts {
        private readonly AiSettings _settings;

        public ConfiguredAiOperationCosts(
            Microsoft.Extensions.Options.IOptions<AiSettings> settings) {
            _settings = settings.Value;
        }

        public long CostOf(string capability, long declaredCost) =>
            _settings.OperationCosts.TryGetValue(capability, out var configured) && configured >= 1
                ? configured
                : Math.Max(1, declaredCost);
    }
}
