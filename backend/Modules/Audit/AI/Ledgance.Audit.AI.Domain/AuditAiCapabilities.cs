using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Domain {
    public sealed record AuditAiCapability(string Key, string RequiredTier, string Description);

    /// <summary>
    /// The catalog of Audit AI capabilities and the AI tier each one requires. This is the single
    /// place capability-to-tier gating is declared; the subscription plan's ai_max_tier decides
    /// which of these an organization can use. Agentic capabilities arrive in Phase 7.
    /// </summary>
    public static class AuditAiCapabilities {
        public static readonly AuditAiCapability Assistant = new(
            "audit.assistant", AiTiers.Basic,
            "Audit assistant and engagement Q&A");

        public static readonly AuditAiCapability DocumentSummary = new(
            "audit.document_summary", AiTiers.Basic,
            "Working-paper, evidence and document summarization");

        public static readonly AuditAiCapability RiskSuggestions = new(
            "audit.risk_suggestions", AiTiers.Advanced,
            "Suggested risks of material misstatement for an engagement");

        public static readonly AuditAiCapability ProcedureSuggestions = new(
            "audit.procedure_suggestions", AiTiers.Advanced,
            "Suggested audit procedures responsive to identified risks");

        public static readonly AuditAiCapability WorkingPaperDraft = new(
            "audit.working_paper_draft", AiTiers.Advanced,
            "Working-paper drafting assistance");

        public static readonly AuditAiCapability FindingDraft = new(
            "audit.finding_draft", AiTiers.Advanced,
            "Finding drafting from observations and evidence");

        public static readonly AuditAiCapability RiskAnalysis = new(
            "audit.risk_analysis", AiTiers.Reasoning,
            "Complex risk and cross-document engagement analysis");

        public static readonly AuditAiCapability AnomalyDetection = new(
            "audit.anomaly_detection", AiTiers.Reasoning,
            "Anomaly detection over the trial balance and engagement records");

        public static readonly AuditAiCapability ReviewAssistance = new(
            "audit.review_assistance", AiTiers.Reasoning,
            "AI-assisted review of engagement completeness and quality");

        public static readonly AuditAiCapability ReportDraft = new(
            "audit.report_draft", AiTiers.Reasoning,
            "Audit report drafting from findings and engagement results");

        public static readonly IReadOnlyList<AuditAiCapability> All = [
            Assistant, DocumentSummary, RiskSuggestions, ProcedureSuggestions,
            WorkingPaperDraft, FindingDraft, RiskAnalysis, AnomalyDetection,
            ReviewAssistance, ReportDraft
        ];
    }
}
