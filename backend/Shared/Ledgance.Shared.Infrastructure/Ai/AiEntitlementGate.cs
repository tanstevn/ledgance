using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Infrastructure.Ai {
    /// <summary>
    /// The plan check every AI workload passes, whether it runs as a single completion or as an
    /// agent loop. Three independent ladders gate it — reasoning tier, report completeness and
    /// analysis breadth — because a plan can buy deeper reasoning without buying a wider view.
    /// A refusal names the capability and what it needs, so the caller can be told what to
    /// upgrade to rather than simply being denied.
    /// </summary>
    internal static class AiEntitlementGate {
        public static void Require(EntitlementSet entitlements, string capability,
            string requiredTier, string requiredReportScope, string requiredAnalysisScope) {
            entitlements.RequireCapability(Entitlements.AiEnabled);

            if (!AiTiers.Allows(entitlements.Tier(Entitlements.AiMaxTier), requiredTier)) {
                throw Refuse(capability, $"the '{requiredTier}' AI tier");
            }

            if (!AiReportScopes.Allows(
                    entitlements.Value(Entitlements.AiReportScope, AiReportScopes.None),
                    requiredReportScope)) {
                throw Refuse(capability, $"'{requiredReportScope}' AI report generation");
            }

            if (!AiAnalysisScopes.Allows(
                    entitlements.Value(Entitlements.AiAnalysisScope, AiAnalysisScopes.Document),
                    requiredAnalysisScope)) {
                throw Refuse(capability, $"'{requiredAnalysisScope}' AI analysis");
            }
        }

        private static EntitlementException Refuse(string capability, string requirement) =>
            EntitlementException.NotIncluded(
                $"AI capability '{capability}' (requires {requirement})");
    }
}
