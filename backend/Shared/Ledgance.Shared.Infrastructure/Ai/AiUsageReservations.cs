using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Ledgance.Shared.Infrastructure.Ai {
    internal sealed record TakenAiUsage(AiUsageReservation Reservation, AiUsageCharge Charge);

    /// <summary>
    /// Taking and giving back AI usage, shared by the completion path and the agent path so both
    /// charge the same way. The allowance is decremented before the work runs — that is what
    /// makes concurrent requests safe — and returned only when nothing reached a provider.
    /// </summary>
    internal static class AiUsageReservations {
        public static async Task<TakenAiUsage> TakeAsync(IAiUsageMeter usage,
            IAiUsagePeriodResolver periods, IAiOperationCosts costs, EntitlementSet entitlements,
            AiUsageContext context, long declaredCost, CancellationToken ct) {
            var period = await periods.ResolveAsync(context.OrganizationId, context.Module, ct);
            var limit = entitlements.Limit(Entitlements.AiMonthlyUnits);
            var units = costs.CostOf(context.Capability, declaredCost);

            var reservation = await usage.TryReserveAsync(context, period, units, limit, ct)
                ?? throw Exhausted(entitlements, await usage.GetAsync(context.OrganizationId,
                    context.Module, period, limit, ct), units);

            var snapshot = reservation.Snapshot(period.ResetsAt);

            return new TakenAiUsage(reservation, new AiUsageCharge(units, snapshot.Remaining,
                snapshot.IsUnlimited, snapshot.IsApproachingLimit, period.ResetsAt));
        }

        public static async Task ReleaseAsync(IAiUsageMeter usage, Guid organizationId,
            TakenAiUsage taken, ILogger logger, CancellationToken ct) {
            try {
                await usage.ReleaseAsync(organizationId, taken.Reservation, ct);
            }
            catch (Exception exception) {
                // The operation already failed; failing again while tidying up would replace a
                // useful error with a meaningless one. The units stay spent, which is the safe
                // direction, and the discrepancy is recorded here.
                logger.LogWarning(exception,
                    "Could not release {Units} reserved AI units for organization " +
                    "{OrganizationId}; they remain consumed.",
                    taken.Reservation.Units, organizationId);
            }
        }

        /// <summary>
        /// The message a user sees when the allowance runs out: what was needed, what is left,
        /// when it refills and what the next plan up carries. No provider, cost or internal
        /// detail appears in it.
        /// </summary>
        private static AiUsageLimitException Exhausted(EntitlementSet entitlements,
            AiUsageSnapshot snapshot, long units) {
            var next = SubscriptionPlanCatalog.NextAbove(entitlements.Plan);

            var upgrade = next is null || SubscriptionPlanCatalog.RequiresContactSales(next.Value)
                ? "Contact us about a plan with more AI capacity."
                : $"{next} includes " +
                  $"{Allowance(SubscriptionPlanCatalog.For(next.Value))} AI credits per period.";

            var resets = snapshot.ResetsAt is { } at
                ? $" Your allowance resets on {at:d MMMM yyyy}."
                : string.Empty;

            return new AiUsageLimitException(
                $"This action needs {units} AI credits and your plan has " +
                $"{snapshot.Remaining} left of {snapshot.Limit} for this period.{resets} " +
                upgrade);
        }

        private static string Allowance(IReadOnlyDictionary<string, string> plan) =>
            plan.TryGetValue(Entitlements.AiMonthlyUnits, out var value)
                && long.TryParse(value, out var units)
                    ? units == EntitlementSet.Unlimited
                        ? "unlimited"
                        : units.ToString("N0", CultureInfo.InvariantCulture)
                    : "more";
    }
}
