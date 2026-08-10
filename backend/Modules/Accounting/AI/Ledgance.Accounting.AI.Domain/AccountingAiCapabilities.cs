using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.AI.Domain {
    public sealed record AccountingAiCapability(string Key, string RequiredTier,
        string Description);

    /// <summary>
    /// The catalog of Accounting AI capabilities and the AI tier each one requires. This is
    /// the single place capability-to-tier gating is declared; the subscription plan's
    /// ai_max_tier decides which of these an organization can use. Agentic capabilities
    /// arrive in Phase 7.
    /// </summary>
    public static class AccountingAiCapabilities {
        public static readonly AccountingAiCapability Assistant = new(
            "accounting.assistant", AiTiers.Basic,
            "Accounting assistant and entity Q&A");

        public static readonly AccountingAiCapability EntryExplanation = new(
            "accounting.entry_explanation", AiTiers.Basic,
            "Plain-language explanation of a journal entry and its effect on the books");

        public static readonly AccountingAiCapability FinancialSummary = new(
            "accounting.financial_summary", AiTiers.Basic,
            "Financial summary of a fiscal period");

        public static readonly AccountingAiCapability EntrySuggestion = new(
            "accounting.entry_suggestion", AiTiers.Advanced,
            "Suggested journal entry for a described transaction, using the entity's chart of accounts");

        public static readonly AccountingAiCapability ReconciliationAssistance = new(
            "accounting.reconciliation_assistance", AiTiers.Advanced,
            "Assistance resolving an in-progress reconciliation's uncleared lines and difference");

        public static readonly AccountingAiCapability StatementExplanation = new(
            "accounting.statement_explanation", AiTiers.Advanced,
            "Explanation of a period's income statement and balance sheet");

        public static readonly AccountingAiCapability VarianceAnalysis = new(
            "accounting.variance_analysis", AiTiers.Advanced,
            "Variance analysis between two fiscal periods");

        public static readonly AccountingAiCapability AnomalyDetection = new(
            "accounting.anomaly_detection", AiTiers.Reasoning,
            "Anomaly detection over the ledger and trial balance");

        public static readonly AccountingAiCapability FinancialAnalysis = new(
            "accounting.financial_analysis", AiTiers.Reasoning,
            "Complex financial analysis of an entity's position and performance");

        public static readonly AccountingAiCapability CloseAssistance = new(
            "accounting.close_assistance", AiTiers.Reasoning,
            "Period-close review: what must be resolved before the period can be closed");

        public static readonly AccountingAiCapability Agent = new(
            "accounting.agent", AiTiers.Agentic,
            "Multi-step agentic investigation across the entity's books");

        public static readonly IReadOnlyList<AccountingAiCapability> All = [
            Assistant, EntryExplanation, FinancialSummary, EntrySuggestion,
            ReconciliationAssistance, StatementExplanation, VarianceAnalysis,
            AnomalyDetection, FinancialAnalysis, CloseAssistance, Agent
        ];
    }
}
