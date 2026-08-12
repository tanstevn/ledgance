using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Application {
    /// <summary>
    /// Every Audit AI result is a proposal: attributed, reviewable, and never applied to the
    /// audit record until a human accepts it through the normal (authorized) commands.
    /// </summary>
    public class AiProposalResult {
        public string Capability { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;

        /// <summary>
        /// What this operation cost and what is left, so a surface can warn before the next
        /// action fails rather than after.
        /// </summary>
        public AiUsageView? Usage { get; set; }

        public string Disclaimer { get; set; } =
            "AI-generated proposal. It assists professional judgment and must be reviewed " +
            "by the engagement team before any use.";

        public static AiProposalResult From(AuditAiCapability capability,
            AiCompletion completion) =>
            new() {
                Capability = capability.Key,
                Content = completion.Content,
                Provider = completion.Provider,
                Model = completion.Model,
                Tier = completion.Tier,
                Usage = AiUsageView.From(completion.Usage)
            };
    }

    public class AiUsageView {
        public long UnitsConsumed { get; set; }
        public long UnitsRemaining { get; set; }
        public bool IsUnlimited { get; set; }
        public bool IsApproachingLimit { get; set; }
        public DateTime? PeriodResetsAt { get; set; }

        public static AiUsageView? From(AiUsageCharge? charge) =>
            charge is null
                ? null
                : new AiUsageView {
                    UnitsConsumed = charge.UnitsConsumed,
                    UnitsRemaining = charge.UnitsRemaining,
                    IsUnlimited = charge.IsUnlimited,
                    IsApproachingLimit = charge.IsApproachingLimit,
                    PeriodResetsAt = charge.PeriodResetsAt
                };
    }

    internal static class AuditAiPrompts {
        public const string SystemBase =
            "You are an AI assistant for a professional audit team working inside the " +
            "Ledgance Audit platform. You support auditors performing financial statement " +
            "and internal audits. Ground every statement in the engagement context you are " +
            "given; when the context does not contain the answer, say so instead of guessing. " +
            "You assist professional judgment — you never replace it, and your output is a " +
            "proposal the engagement team must review. Be precise, structured and concise.";

        /// <summary>
        /// Added to any prompt that produces report content. An audit report carries
        /// professional responsibility, so the model is told plainly what it must not invent
        /// and what it must hand back to the auditor unresolved.
        /// </summary>
        public const string ReportingDiscipline =
            "This is report content and is held to report standards. Never invent evidence, " +
            "procedures, findings, amounts, client details, documentation or conclusions. " +
            "Use only what the engagement context contains. Where the context lacks something " +
            "the section needs, write '[NOT IN THE ENGAGEMENT RECORD: <what is missing>]' and " +
            "continue — do not fill the gap with plausible text. Reserve every audit opinion " +
            "and every conclusion of record for the engagement partner and mark those places " +
            "'[PARTNER JUDGMENT]'. Cite the engagement records each section rests on.";

        public static AiWorkload Workload(AuditAiCapability capability, string instruction,
            string userPrompt, IReadOnlyList<AiDocument>? context = null,
            Guid? engagementId = null, Guid? clientId = null) =>
            AiWorkload.For(ProductModule.Audit, capability.Key, capability.RequiredTier,
                $"{SystemBase}\n\n{instruction}", userPrompt, context,
                capability.RequiredReportScope, capability.RequiredAnalysisScope,
                capability.Cost, clientId, engagementId);

        public static AiWorkload ReportWorkload(AuditAiCapability capability, string instruction,
            string userPrompt, IReadOnlyList<AiDocument>? context = null,
            Guid? engagementId = null, Guid? clientId = null) =>
            Workload(capability, $"{instruction}\n\n{ReportingDiscipline}", userPrompt, context,
                engagementId, clientId);
    }

    /// <summary>
    /// Builds AI context documents from engagement data the caller is already authorized to
    /// read. This is the only material AI sees — there is no privileged AI data path.
    /// </summary>
    internal static class EngagementAiContext {
        public static async Task<List<AiDocument>> OverviewAsync(Guid engagementId,
            IEngagementRepository engagements, IClientLookup clients, CancellationToken ct) {
            var documents = new List<AiDocument>();
            var engagement = await engagements.FindAsync(engagementId, ct);

            if (engagement is null) {
                return documents;
            }

            var names = await clients.GetNamesAsync([engagement.ClientId], ct);

            var materiality = engagement.Materiality is null
                ? "Materiality has not been determined yet."
                : $"Overall materiality {engagement.Materiality.OverallAmount:N2}, " +
                  $"performance materiality {engagement.Materiality.PerformanceAmount:N2}, " +
                  $"clearly trivial threshold {engagement.Materiality.ClearlyTrivialThreshold:N2} " +
                  $"(basis: {engagement.Materiality.Basis}).";

            var plan = engagement.Plan is null
                ? "No audit plan has been documented yet."
                : $"Scope: {engagement.Plan.Scope}\nObjectives: {engagement.Plan.Objectives}\n" +
                  $"Strategy: {engagement.Plan.Strategy}\n" +
                  $"Plan approved: {engagement.Plan.IsApproved}";

            documents.Add(new AiDocument("Engagement overview",
                $"Engagement: {engagement.Name}\n" +
                $"Client: {names.GetValueOrDefault(engagement.ClientId, "Unknown")}\n" +
                $"Type: {engagement.Type}\nStatus: {engagement.Status}\n" +
                $"Period: {engagement.PeriodStart} to {engagement.PeriodEnd}\n" +
                $"{materiality}\n\nAudit plan:\n{plan}"));

            return documents;
        }

        public static async Task<AiDocument?> RisksAsync(Guid engagementId,
            IRiskRepository risks, CancellationToken ct) {
            var items = await risks.ListAsync(engagementId, ct);

            if (items.Count == 0) {
                return null;
            }

            var lines = items.Select(risk =>
                $"- [{risk.Level}] {risk.Title}: {risk.Description} " +
                $"(assertions: {risk.Assertions}; planned response: {risk.PlannedResponse})");

            return new AiDocument("Identified risks", string.Join('\n', lines));
        }

        public static async Task<AiDocument?> ProceduresAsync(Guid engagementId,
            IProcedureRepository procedures, CancellationToken ct) {
            var items = await procedures.ListAsync(engagementId, ct);

            if (items.Count == 0) {
                return null;
            }

            var lines = items.Select(procedure =>
                $"- [{procedure.Status}] {procedure.Area} / {procedure.Title}: " +
                $"{procedure.Description}" +
                (procedure.Conclusion is null ? "" : $" Conclusion: {procedure.Conclusion}"));

            return new AiDocument("Audit procedures", string.Join('\n', lines));
        }

        public static async Task<AiDocument?> FindingsAsync(Guid engagementId,
            IFindingRepository findings, CancellationToken ct) {
            var items = await findings.ListAsync(engagementId, ct);

            if (items.Count == 0) {
                return null;
            }

            var lines = items.Select(finding =>
                $"- [{finding.Severity}/{finding.Status}] {finding.Title}: " +
                $"{finding.Description} Recommendation: {finding.Recommendation}" +
                (finding.Resolution is null ? "" : $" Resolution: {finding.Resolution}"));

            return new AiDocument("Findings", string.Join('\n', lines));
        }

        public static async Task<AiDocument?> WorkingPapersAsync(Guid engagementId,
            IWorkingPaperRepository papers, CancellationToken ct) {
            var items = await papers.ListAsync(engagementId, ct);

            if (items.Count == 0) {
                return null;
            }

            var lines = items.Select(paper =>
                $"- [{paper.Status}] {paper.Reference} {paper.Title} " +
                $"(open review notes: {paper.OpenNoteCount})");

            return new AiDocument("Working papers", string.Join('\n', lines));
        }

        public static async Task<AiDocument?> EvidenceAsync(Guid engagementId,
            IEvidenceRepository evidence, CancellationToken ct) {
            var items = await evidence.ListAsync(engagementId, ct);

            if (items.Count == 0) {
                return null;
            }

            var lines = items.Select(item =>
                $"- [{item.Category}] {item.FileName} (v{item.Version}, {item.SizeBytes} bytes)" +
                $" linked to working paper {item.WorkingPaperId?.ToString() ?? "none"}," +
                $" procedure {item.ProcedureId?.ToString() ?? "none"}: {item.Description}");

            return new AiDocument("Evidence register", string.Join('\n', lines));
        }

        public static async Task<AiDocument?> TrialBalanceAsync(Guid engagementId,
            ITrialBalanceRepository trialBalances, CancellationToken ct) {
            var import = await trialBalances.FindLatestAsync(engagementId, ct);

            if (import is null) {
                return null;
            }

            var lines = import.Lines.Select(line =>
                $"{line.AccountCode}\t{line.AccountName}\t{line.Debit:N2}\t{line.Credit:N2}");

            return new AiDocument(
                $"Trial balance ({import.PeriodLabel}, source {import.Source}, " +
                $"{(import.IsBalanced ? "balanced" : "OUT OF BALANCE")})",
                "AccountCode\tAccountName\tDebit\tCredit\n" + string.Join('\n', lines));
        }

        /// <summary>
        /// The whole engagement record in one pass. Reads that do not depend on each other run
        /// together, because each one is a separate network round trip.
        /// </summary>
        public static async Task<List<AiDocument>> FullAsync(Guid engagementId,
            EngagementReadSet reads, CancellationToken ct) {
            var overview = OverviewAsync(engagementId, reads.Engagements, reads.Clients, ct);
            var risks = RisksAsync(engagementId, reads.Risks, ct);
            var procedures = ProceduresAsync(engagementId, reads.Procedures, ct);
            var findings = FindingsAsync(engagementId, reads.Findings, ct);
            var papers = WorkingPapersAsync(engagementId, reads.WorkingPapers, ct);
            var evidence = EvidenceAsync(engagementId, reads.Evidence, ct);
            var trialBalance = TrialBalanceAsync(engagementId, reads.TrialBalances, ct);

            await Task.WhenAll(overview, risks, procedures, findings, papers, evidence,
                trialBalance);

            var documents = new List<AiDocument>(overview.Result);

            foreach (var document in new[] {
                risks.Result, procedures.Result, findings.Result, papers.Result,
                evidence.Result, trialBalance.Result
            }) {
                if (document is not null) {
                    documents.Add(document);
                }
            }

            return documents;
        }
    }

    /// <summary>
    /// The engagement repositories a whole-record read needs, grouped so handlers that assemble
    /// full report context take one dependency instead of seven.
    /// </summary>
    public sealed class EngagementReadSet {
        public EngagementReadSet(IEngagementRepository engagements, IClientLookup clients,
            IRiskRepository risks, IProcedureRepository procedures,
            IWorkingPaperRepository workingPapers, IEvidenceRepository evidence,
            IFindingRepository findings, ITrialBalanceRepository trialBalances) {
            Engagements = engagements;
            Clients = clients;
            Risks = risks;
            Procedures = procedures;
            WorkingPapers = workingPapers;
            Evidence = evidence;
            Findings = findings;
            TrialBalances = trialBalances;
        }

        public IEngagementRepository Engagements { get; }
        public IClientLookup Clients { get; }
        public IRiskRepository Risks { get; }
        public IProcedureRepository Procedures { get; }
        public IWorkingPaperRepository WorkingPapers { get; }
        public IEvidenceRepository Evidence { get; }
        public IFindingRepository Findings { get; }
        public ITrialBalanceRepository TrialBalances { get; }
    }
}
