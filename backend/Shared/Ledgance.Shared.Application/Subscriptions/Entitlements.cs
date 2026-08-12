namespace Ledgance.Shared.Application.Subscriptions {
    public static class Entitlements {
        public const string MaxUsers = "max_users";
        public const string MaxClients = "max_clients";
        public const string MaxEngagements = "max_engagements";
        public const string MaxEntities = "max_entities";
        public const string MaxTransactionsPerPeriod = "max_transactions_per_period";
        public const string StorageBytes = "storage_bytes";

        public const string AiEnabled = "ai_enabled";
        public const string AiMonthlyUnits = "ai_monthly_units";
        public const string AiMaxTier = "ai_max_tier";
        public const string AiMaxContextTokens = "ai_max_context_tokens";

        /// <summary>How complete an AI-generated report the plan may produce.</summary>
        public const string AiReportScope = "ai_report_scope";

        /// <summary>How far across the record set AI may reason in one request.</summary>
        public const string AiAnalysisScope = "ai_analysis_scope";

        public const string AdvancedAnalysis = "advanced_analysis";
        public const string AdvancedReview = "advanced_review";
        public const string Automation = "automation";
        public const string Integrations = "integrations";
        public const string ApiAccess = "api_access";
        public const string AccountingContextSharing = "accounting_context_sharing";
        public const string EnterpriseSupport = "enterprise_support";
    }

    /// <summary>
    /// Ordered entitlements below: a plan grants one level, a capability requires one, and the
    /// plan satisfies the capability when its level ranks at least as high. A value outside the
    /// ladder ranks below every level, so an unrecognised grant denies rather than escalates.
    /// </summary>
    public static class AiTiers {
        public const string Basic = "basic";
        public const string Advanced = "advanced";
        public const string Reasoning = "reasoning";
        public const string Agentic = "agentic";

        private static readonly string[] Ranked = [Basic, Advanced, Reasoning, Agentic];

        public static int RankOf(string tier) =>
            Array.IndexOf(Ranked, tier);

        public static bool Allows(string permittedTier, string requestedTier) =>
            RankOf(permittedTier) >= 0
            && RankOf(permittedTier) >= RankOf(requestedTier);
    }

    /// <summary>
    /// The report-generation progression. Each level is a genuinely different product
    /// capability rather than a larger quota: sections, then a whole report, then the whole
    /// engagement, then across engagements, then an agent that assembles and checks its own
    /// draft, then customer-specific templates and methodology.
    /// </summary>
    public static class AiReportScopes {
        public const string None = "none";
        public const string Sections = "sections";
        public const string FullDraft = "full_draft";
        public const string Engagement = "engagement";
        public const string Portfolio = "portfolio";
        public const string Agentic = "agentic";
        public const string Custom = "custom";

        private static readonly string[] Ranked =
            [None, Sections, FullDraft, Engagement, Portfolio, Agentic, Custom];

        public static int RankOf(string scope) =>
            Array.IndexOf(Ranked, scope);

        public static bool Allows(string grantedScope, string requiredScope) =>
            RankOf(grantedScope) >= 0
            && RankOf(grantedScope) >= RankOf(requiredScope);
    }

    /// <summary>
    /// How wide a view AI may reason over: one document, one engagement, a multi-step workflow
    /// within an engagement, or across the engagements the caller is authorized to see.
    /// Authorization is enforced separately and always — this only caps the breadth a plan buys.
    /// </summary>
    public static class AiAnalysisScopes {
        public const string Document = "document";
        public const string Engagement = "engagement";
        public const string Workflow = "workflow";
        public const string Portfolio = "portfolio";

        private static readonly string[] Ranked = [Document, Engagement, Workflow, Portfolio];

        public static int RankOf(string scope) =>
            Array.IndexOf(Ranked, scope);

        public static bool Allows(string grantedScope, string requiredScope) =>
            RankOf(grantedScope) >= 0
            && RankOf(grantedScope) >= RankOf(requiredScope);
    }
}
