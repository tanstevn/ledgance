namespace Ledgance.Shared.Application.Subscriptions {
    /// <summary>
    /// The only place plan-to-entitlement values are declared. Configuration under
    /// "Subscriptions:Plans:&lt;PlanCode&gt;:&lt;entitlement&gt;" overrides these defaults, and a
    /// per-organization override may in turn override configuration (negotiated Enterprise terms).
    /// </summary>
    public static class SubscriptionPlanCatalog {
        private const string Unlimited = "-1";
        private const string Yes = "true";
        private const string No = "false";

        private static readonly Dictionary<PlanCode, IReadOnlyDictionary<string, string>> Defaults = new() {
            [PlanCode.Free] = Plan(
                users: "3", clients: "1", engagements: "2", entities: "1", transactions: "300",
                storage: Gb(5), aiUnits: "200", aiTier: AiTiers.Basic, aiContext: "16000",
                reportScope: AiReportScopes.None, analysisScope: AiAnalysisScopes.Document,
                advancedAnalysis: No, advancedReview: No, automation: No,
                integrations: No, apiAccess: No, contextSharing: No, enterpriseSupport: No),

            [PlanCode.AuditMicro] = Plan(
                users: "15", clients: "30", engagements: "75", entities: "0", transactions: "0",
                storage: Gb(250), aiUnits: "12000", aiTier: AiTiers.Advanced, aiContext: "128000",
                reportScope: AiReportScopes.Sections, analysisScope: AiAnalysisScopes.Document,
                advancedAnalysis: No, advancedReview: No, automation: No,
                integrations: Yes, apiAccess: No, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AuditMicroGrowth] = Plan(
                users: "40", clients: "100", engagements: "300", entities: "0", transactions: "0",
                storage: Gb(500), aiUnits: "40000", aiTier: AiTiers.Advanced, aiContext: "200000",
                reportScope: AiReportScopes.FullDraft, analysisScope: AiAnalysisScopes.Engagement,
                advancedAnalysis: Yes, advancedReview: No, automation: No,
                integrations: Yes, apiAccess: No, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AuditSmall] = Plan(
                users: "90", clients: "250", engagements: "800", entities: "0", transactions: "0",
                storage: Gb(750), aiUnits: "120000", aiTier: AiTiers.Reasoning, aiContext: "200000",
                reportScope: AiReportScopes.Engagement, analysisScope: AiAnalysisScopes.Workflow,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AuditMedium] = Plan(
                users: "150", clients: "500", engagements: "1300", entities: "0", transactions: "0",
                storage: Gb(2048), aiUnits: "300000", aiTier: AiTiers.Reasoning, aiContext: "200000",
                reportScope: AiReportScopes.Portfolio, analysisScope: AiAnalysisScopes.Portfolio,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AuditMediumGrowth] = Plan(
                users: "200", clients: Unlimited, engagements: Unlimited, entities: "0", transactions: "0",
                storage: Gb(6144), aiUnits: "750000", aiTier: AiTiers.Agentic, aiContext: "200000",
                reportScope: AiReportScopes.Agentic, analysisScope: AiAnalysisScopes.Portfolio,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AuditEnterprise] = Plan(
                users: Unlimited, clients: Unlimited, engagements: Unlimited, entities: "0", transactions: "0",
                storage: Unlimited, aiUnits: Unlimited, aiTier: AiTiers.Agentic, aiContext: "200000",
                reportScope: AiReportScopes.Custom, analysisScope: AiAnalysisScopes.Portfolio,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: Yes),

            [PlanCode.AccountingSolo] = Plan(
                users: "3", clients: "0", engagements: "0", entities: "3", transactions: "5000",
                storage: Gb(10), aiUnits: "1500", aiTier: AiTiers.Advanced, aiContext: "128000",
                reportScope: AiReportScopes.Sections, analysisScope: AiAnalysisScopes.Document,
                advancedAnalysis: No, advancedReview: No, automation: No,
                integrations: No, apiAccess: No, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AccountingTeam] = Plan(
                users: "10", clients: "0", engagements: "0", entities: "10", transactions: "25000",
                storage: Gb(100), aiUnits: "8000", aiTier: AiTiers.Advanced, aiContext: "200000",
                reportScope: AiReportScopes.Sections, analysisScope: AiAnalysisScopes.Engagement,
                advancedAnalysis: Yes, advancedReview: No, automation: Yes,
                integrations: Yes, apiAccess: No, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AccountingProfessional] = Plan(
                users: "30", clients: "0", engagements: "0", entities: "50", transactions: "150000",
                storage: Gb(500), aiUnits: "25000", aiTier: AiTiers.Reasoning, aiContext: "200000",
                reportScope: AiReportScopes.FullDraft, analysisScope: AiAnalysisScopes.Workflow,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: No),

            [PlanCode.AccountingEnterprise] = Plan(
                users: Unlimited, clients: "0", engagements: "0", entities: Unlimited, transactions: Unlimited,
                storage: Unlimited, aiUnits: Unlimited, aiTier: AiTiers.Agentic, aiContext: "200000",
                reportScope: AiReportScopes.Custom, analysisScope: AiAnalysisScopes.Portfolio,
                advancedAnalysis: Yes, advancedReview: Yes, automation: Yes,
                integrations: Yes, apiAccess: Yes, contextSharing: Yes, enterpriseSupport: Yes)
        };

        /// <summary>
        /// Cheapest first within each module. Upgrade surfaces read this rather than the enum
        /// order so "the next plan up" stays a catalogue fact.
        /// </summary>
        private static readonly PlanCode[] Progression = [
            PlanCode.Free,
            PlanCode.AuditMicro, PlanCode.AuditMicroGrowth, PlanCode.AuditSmall,
            PlanCode.AuditMedium, PlanCode.AuditMediumGrowth, PlanCode.AuditEnterprise,
            PlanCode.AccountingSolo, PlanCode.AccountingTeam,
            PlanCode.AccountingProfessional, PlanCode.AccountingEnterprise
        ];

        public static IReadOnlyDictionary<PlanCode, IReadOnlyDictionary<string, string>> All => Defaults;

        public static IReadOnlyDictionary<string, string> For(PlanCode plan) =>
            Defaults.TryGetValue(plan, out var values)
                ? values
                : Defaults[PlanCode.Free];

        public static bool RequiresContactSales(PlanCode plan) =>
            plan is PlanCode.AuditEnterprise or PlanCode.AccountingEnterprise;

        public static ProductModule ModuleOf(PlanCode plan) =>
            plan.ToString().StartsWith("Accounting", StringComparison.Ordinal)
                ? ProductModule.Accounting
                : ProductModule.Audit;

        /// <summary>
        /// The plans of one module, cheapest first, with the shared Free plan leading.
        /// </summary>
        public static IReadOnlyList<PlanCode> Ordered(ProductModule module) => [.. Progression
            .Where(plan => plan == PlanCode.Free || ModuleOf(plan) == module)];

        /// <summary>
        /// The next plan up from <paramref name="plan"/> in its own module, or null at the top.
        /// </summary>
        public static PlanCode? NextAbove(PlanCode plan) {
            var ordered = Ordered(ModuleOf(plan));
            var index = ordered.ToList().IndexOf(plan);

            return index >= 0 && index + 1 < ordered.Count
                ? ordered[index + 1]
                : null;
        }

        private static string Gb(long gigabytes) =>
            (gigabytes * 1024L * 1024 * 1024).ToString();

        private static Dictionary<string, string> Plan(string users, string clients,
            string engagements, string entities, string transactions, string storage,
            string aiUnits, string aiTier, string aiContext, string reportScope,
            string analysisScope, string advancedAnalysis, string advancedReview,
            string automation, string integrations, string apiAccess,
            string contextSharing, string enterpriseSupport) => new() {
                [Entitlements.MaxUsers] = users,
                [Entitlements.MaxClients] = clients,
                [Entitlements.MaxEngagements] = engagements,
                [Entitlements.MaxEntities] = entities,
                [Entitlements.MaxTransactionsPerPeriod] = transactions,
                [Entitlements.StorageBytes] = storage,
                [Entitlements.AiEnabled] = Yes,
                [Entitlements.AiMonthlyUnits] = aiUnits,
                [Entitlements.AiMaxTier] = aiTier,
                [Entitlements.AiMaxContextTokens] = aiContext,
                [Entitlements.AiReportScope] = reportScope,
                [Entitlements.AiAnalysisScope] = analysisScope,
                [Entitlements.AdvancedAnalysis] = advancedAnalysis,
                [Entitlements.AdvancedReview] = advancedReview,
                [Entitlements.Automation] = automation,
                [Entitlements.Integrations] = integrations,
                [Entitlements.ApiAccess] = apiAccess,
                [Entitlements.AccountingContextSharing] = contextSharing,
                [Entitlements.EnterpriseSupport] = enterpriseSupport
            };
    }
}
