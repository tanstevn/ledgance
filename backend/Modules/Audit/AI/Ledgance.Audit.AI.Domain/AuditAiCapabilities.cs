using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Domain {
    /// <summary>
    /// One Audit AI capability and what it consumes: the three entitlement levels it needs — how
    /// hard the model has to think, how complete a report it may produce, and how far across the
    /// record set it may reason — plus its cost in AI credits. A plan includes the capability
    /// only when it grants all three levels, and each use spends <see cref="Cost"/> credits from
    /// the plan's allowance.
    /// </summary>
    public sealed record AuditAiCapability(
        string Key,
        string RequiredTier,
        string Description,
        string RequiredReportScope = AiReportScopes.None,
        string RequiredAnalysisScope = AiAnalysisScopes.Document,
        long Cost = 1);

    /// <summary>
    /// The catalog of Audit AI capabilities. This is the single place capability-to-entitlement
    /// gating and AI-credit cost are declared; the subscription plan decides which of these an
    /// organization can use and how many credits it has to spend on them.
    ///
    /// Costs are a product currency, not a provider one: they express how much of an
    /// organization's allowance an operation is worth, so changing which model serves a tier
    /// never changes what a customer is charged. Configuration
    /// (<c>Ai:OperationCosts:&lt;key&gt;</c>) overrides any value here.
    /// </summary>
    public static class AuditAiCapabilities {
        public static readonly AuditAiCapability Assistant = new(
            "audit.assistant", AiTiers.Basic,
            "Audit assistant and engagement Q&A",
            Cost: 1);

        public static readonly AuditAiCapability DocumentSummary = new(
            "audit.document_summary", AiTiers.Basic,
            "Working-paper, evidence and document summarization",
            Cost: 2);

        public static readonly AuditAiCapability FindingSummary = new(
            "audit.finding_summary", AiTiers.Basic,
            "Plain-language summaries of the findings raised on an engagement",
            Cost: 1);

        public static readonly AuditAiCapability EngagementSummary = new(
            "audit.engagement_summary", AiTiers.Basic,
            "Short status summary of where an engagement stands",
            Cost: 1);

        public static readonly AuditAiCapability NoteDraft = new(
            "audit.note_draft", AiTiers.Basic,
            "Engagement notes written up from an auditor's rough observation",
            Cost: 1);

        public static readonly AuditAiCapability WordingAssistance = new(
            "audit.wording_assistance", AiTiers.Basic,
            "Rewording a passage of working-paper text for clarity and tone",
            Cost: 1);

        public static readonly AuditAiCapability PlanAssistance = new(
            "audit.plan_assistance", AiTiers.Advanced,
            "Audit planning assistance: scope, objectives and strategy",
            Cost: 3);

        public static readonly AuditAiCapability MaterialityAssistance = new(
            "audit.materiality_assistance", AiTiers.Advanced,
            "Materiality benchmark and threshold assistance",
            Cost: 3);

        public static readonly AuditAiCapability RiskSuggestions = new(
            "audit.risk_suggestions", AiTiers.Advanced,
            "Suggested and categorized risks of material misstatement",
            Cost: 3);

        public static readonly AuditAiCapability ProcedureSuggestions = new(
            "audit.procedure_suggestions", AiTiers.Advanced,
            "Suggested audit procedures responsive to identified risks",
            Cost: 3);

        public static readonly AuditAiCapability WorkingPaperDraft = new(
            "audit.working_paper_draft", AiTiers.Advanced,
            "Structured working-paper drafting assistance",
            Cost: 4);

        public static readonly AuditAiCapability FindingDraft = new(
            "audit.finding_draft", AiTiers.Advanced,
            "Finding drafting from observations and evidence",
            Cost: 3);

        public static readonly AuditAiCapability ReportSection = new(
            "audit.report_section", AiTiers.Advanced,
            "Drafting an individual audit report section",
            AiReportScopes.Sections, Cost: 6);

        public static readonly AuditAiCapability EvidenceAnalysis = new(
            "audit.evidence_analysis", AiTiers.Advanced,
            "Evidence coverage and gap analysis across the engagement",
            AiReportScopes.None, AiAnalysisScopes.Engagement, Cost: 8);

        public static readonly AuditAiCapability EngagementIntelligence = new(
            "audit.engagement_intelligence", AiTiers.Advanced,
            "Reasoning across everything recorded on one engagement at once",
            AiReportScopes.None, AiAnalysisScopes.Engagement, Cost: 8);

        public static readonly AuditAiCapability ReportDraft = new(
            "audit.report_draft", AiTiers.Advanced,
            "A complete draft audit report assembled from the engagement record",
            AiReportScopes.FullDraft, AiAnalysisScopes.Engagement, Cost: 20);

        public static readonly AuditAiCapability ReportConsistency = new(
            "audit.report_consistency", AiTiers.Advanced,
            "Checking a report draft against the engagement record for contradictions",
            AiReportScopes.FullDraft, AiAnalysisScopes.Engagement, Cost: 10);

        public static readonly AuditAiCapability RiskAnalysis = new(
            "audit.risk_analysis", AiTiers.Reasoning,
            "Complex risk and cross-document engagement analysis",
            AiReportScopes.None, AiAnalysisScopes.Workflow, Cost: 12);

        public static readonly AuditAiCapability AnomalyDetection = new(
            "audit.anomaly_detection", AiTiers.Reasoning,
            "Anomaly detection over the trial balance and engagement records",
            AiReportScopes.None, AiAnalysisScopes.Workflow, Cost: 12);

        public static readonly AuditAiCapability ReviewAssistance = new(
            "audit.review_assistance", AiTiers.Reasoning,
            "AI-assisted review of engagement completeness and quality",
            AiReportScopes.None, AiAnalysisScopes.Workflow, Cost: 12);

        public static readonly AuditAiCapability EngagementReport = new(
            "audit.engagement_report", AiTiers.Reasoning,
            "Full engagement reporting: management summary and reviewer-oriented draft",
            AiReportScopes.Engagement, AiAnalysisScopes.Workflow, Cost: 35);

        public static readonly AuditAiCapability PortfolioIntelligence = new(
            "audit.portfolio_intelligence", AiTiers.Reasoning,
            "Client and firm intelligence across the engagements the caller may see",
            AiReportScopes.None, AiAnalysisScopes.Portfolio, Cost: 25);

        public static readonly AuditAiCapability PortfolioReport = new(
            "audit.portfolio_report", AiTiers.Reasoning,
            "Multi-engagement, client and firm-level reporting",
            AiReportScopes.Portfolio, AiAnalysisScopes.Portfolio, Cost: 40);

        public static readonly AuditAiCapability Agent = new(
            "audit.agent", AiTiers.Agentic,
            "Multi-step agentic investigation across the engagement's records",
            AiReportScopes.None, AiAnalysisScopes.Portfolio, Cost: 50);

        public static readonly AuditAiCapability AgenticReport = new(
            "audit.agentic_report", AiTiers.Agentic,
            "Agentic report generation: the agent gathers, drafts, then checks its own draft",
            AiReportScopes.Agentic, AiAnalysisScopes.Portfolio, Cost: 80);

        public static readonly IReadOnlyList<AuditAiCapability> All = [
            Assistant, DocumentSummary, FindingSummary, EngagementSummary, NoteDraft,
            WordingAssistance, PlanAssistance, MaterialityAssistance, RiskSuggestions,
            ProcedureSuggestions, WorkingPaperDraft, FindingDraft, ReportSection,
            EvidenceAnalysis, EngagementIntelligence, ReportDraft, ReportConsistency,
            RiskAnalysis, AnomalyDetection, ReviewAssistance, EngagementReport,
            PortfolioIntelligence, PortfolioReport, Agent, AgenticReport
        ];
    }

    /// <summary>
    /// The sections an AI-generated audit report is composed of. Which of these a plan may
    /// generate follows from its report scope, not from this list.
    /// </summary>
    public enum AuditReportSection {
        ExecutiveSummary,
        Scope,
        Approach,
        Materiality,
        RiskAssessment,
        ProceduresPerformed,
        EvidenceSummary,
        Findings,
        Recommendations,
        BasisForOpinion,
        KeyAuditMatters,
        ManagementSummary,
        Conclusion
    }
}
