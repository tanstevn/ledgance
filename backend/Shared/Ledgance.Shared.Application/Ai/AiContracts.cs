using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Application.Ai {
    public sealed record AiDocument(string Title, string Content);

    /// <summary>
    /// A provider-agnostic unit of AI work. Features describe the task and its required tier;
    /// the orchestrator decides which provider and model serve it.
    /// </summary>
    public sealed record AiWorkload(
        ProductModule Module,
        string Capability,
        string RequiredTier,
        string SystemPrompt,
        string UserPrompt,
        IReadOnlyList<AiDocument> Context) {
        public static AiWorkload For(ProductModule module, string capability,
            string requiredTier, string systemPrompt, string userPrompt,
            IReadOnlyList<AiDocument>? context = null) =>
            new(module, capability, requiredTier, systemPrompt, userPrompt, context ?? []);
    }

    public sealed record AiCompletion(
        string Content,
        string Provider,
        string Model,
        string Tier,
        int EstimatedContextTokens);

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

    /// <summary>
    /// Monthly AI usage per organization and module, in units (one unit per completion).
    /// </summary>
    public interface IAiUsageMeter {
        Task<long> GetUsedAsync(Guid organizationId, ProductModule module, string period,
            CancellationToken ct);

        Task RecordAsync(Guid organizationId, ProductModule module, string period,
            long units, CancellationToken ct);
    }

    public static class AiUsage {
        public static string CurrentPeriod() =>
            DateTime.UtcNow.ToString("yyyy-MM");

        /// <summary>
        /// Rough token estimate (~4 characters per token) used for the context-size entitlement
        /// gate. Exact counts are provider-specific and not needed for limit enforcement.
        /// </summary>
        public static int EstimateTokens(string text) =>
            (text.Length + 3) / 4;
    }
}
