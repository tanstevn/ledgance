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
                Tier = completion.Tier
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

        public static AiWorkload Workload(AuditAiCapability capability, string instruction,
            string userPrompt, IReadOnlyList<AiDocument>? context = null) =>
            AiWorkload.For(ProductModule.Audit, capability.Key, capability.RequiredTier,
                $"{SystemBase}\n\n{instruction}", userPrompt, context);
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
    }
}
