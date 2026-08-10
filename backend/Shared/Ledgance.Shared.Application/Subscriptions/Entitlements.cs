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

        public const string AdvancedAnalysis = "advanced_analysis";
        public const string AdvancedReview = "advanced_review";
        public const string Automation = "automation";
        public const string Integrations = "integrations";
        public const string ApiAccess = "api_access";
        public const string AccountingContextSharing = "accounting_context_sharing";
        public const string EnterpriseSupport = "enterprise_support";
    }

    public static class AiTiers {
        public const string Basic = "basic";
        public const string Advanced = "advanced";
        public const string Reasoning = "reasoning";
        public const string Agentic = "agentic";

        private static readonly string[] Ranked = [Basic, Advanced, Reasoning, Agentic];

        public static int RankOf(string tier) =>
            Array.IndexOf(Ranked, tier);

        public static bool Allows(string permittedTier, string requestedTier) =>
            RankOf(permittedTier) >= RankOf(requestedTier);
    }
}
