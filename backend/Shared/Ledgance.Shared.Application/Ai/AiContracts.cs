using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Application.Ai {
    public sealed record AiDocument(string Title, string Content);

    /// <summary>
    /// A provider-agnostic unit of AI work. Features describe the task, the reasoning tier it
    /// needs and how much report and analysis breadth it consumes; the orchestrator decides
    /// which provider and model serve it, and refuses what the plan does not include.
    /// </summary>
    public sealed record AiWorkload(
        ProductModule Module,
        string Capability,
        string RequiredTier,
        string SystemPrompt,
        string UserPrompt,
        IReadOnlyList<AiDocument> Context,
        string RequiredReportScope = AiReportScopes.None,
        string RequiredAnalysisScope = AiAnalysisScopes.Document,
        long Cost = 1,
        Guid? ClientId = null,
        Guid? EngagementId = null) {
        public static AiWorkload For(ProductModule module, string capability,
            string requiredTier, string systemPrompt, string userPrompt,
            IReadOnlyList<AiDocument>? context = null,
            string requiredReportScope = AiReportScopes.None,
            string requiredAnalysisScope = AiAnalysisScopes.Document,
            long cost = 1, Guid? clientId = null, Guid? engagementId = null) =>
            new(module, capability, requiredTier, systemPrompt, userPrompt, context ?? [],
                requiredReportScope, requiredAnalysisScope, cost, clientId, engagementId);
    }

    public sealed record AiCompletion(
        string Content,
        string Provider,
        string Model,
        string Tier,
        int EstimatedContextTokens,
        AiUsageCharge? Usage = null);

    /// <summary>
    /// What the operation cost and what is left, so a caller can tell the user without a second
    /// round trip. It carries no provider or cost-of-goods detail — units are a product measure.
    /// </summary>
    public sealed record AiUsageCharge(
        long UnitsConsumed,
        long UnitsRemaining,
        bool IsUnlimited,
        bool IsApproachingLimit,
        DateTime? PeriodResetsAt);

    /// <summary>
    /// The single entry point features use. Implementations must enforce authorization,
    /// entitlements, usage accounting and provider routing — features never talk to a provider.
    /// </summary>
    public interface IAiCompletionService {
        Task<AiCompletion> CompleteAsync(AiWorkload workload, CancellationToken ct);
    }

    public sealed record AiModelRoute(string Provider, string Model, int MaxOutputTokens);

    public interface IAiModelRouter {
        AiModelRoute Resolve(string tier);
    }

    public interface IAiChatClient {
        string Provider { get; }

        Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
            int maxOutputTokens, CancellationToken ct);
    }

}
