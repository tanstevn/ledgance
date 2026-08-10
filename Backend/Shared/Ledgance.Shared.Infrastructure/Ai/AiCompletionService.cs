using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Ledgance.Shared.Infrastructure.Ai {
    /// <summary>
    /// The only path to an AI provider. Order matters: authorization → entitlements (tier, usage,
    /// context size) → routing → execution → usage recording. A workload above the plan's tier is
    /// refused with an upgrade-relevant error, never silently escalated; a provider failure falls
    /// back down the tier chain, never up.
    /// </summary>
    public sealed class AiCompletionService : IAiCompletionService {
        private static readonly string[] TierOrder =
            [AiTiers.Agentic, AiTiers.Reasoning, AiTiers.Advanced, AiTiers.Basic];

        private readonly ICurrentUserAccessor _currentUser;
        private readonly IEntitlementService _entitlements;
        private readonly IAiUsageMeter _usage;
        private readonly IAiModelRouter _router;
        private readonly IReadOnlyDictionary<string, IAiChatClient> _clients;
        private readonly ILogger<AiCompletionService> _logger;

        public AiCompletionService(ICurrentUserAccessor currentUser,
            IEntitlementService entitlements, IAiUsageMeter usage, IAiModelRouter router,
            IEnumerable<IAiChatClient> clients, ILogger<AiCompletionService> logger) {
            _currentUser = currentUser;
            _entitlements = entitlements;
            _usage = usage;
            _router = router;
            _clients = clients.ToDictionary(client => client.Provider);
            _logger = logger;
        }

        public async Task<AiCompletion> CompleteAsync(AiWorkload workload, CancellationToken ct) {
            var user = _currentUser.Require();

            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                workload.Module, ct);

            entitlements.RequireCapability(Entitlements.AiEnabled);

            var permittedTier = entitlements.Tier(Entitlements.AiMaxTier);

            if (!AiTiers.Allows(permittedTier, workload.RequiredTier)) {
                throw EntitlementException.NotIncluded(
                    $"AI capability '{workload.Capability}' (requires the " +
                    $"'{workload.RequiredTier}' AI tier)");
            }

            var period = AiUsage.CurrentPeriod();
            var used = await _usage.GetUsedAsync(user.OrganizationId, workload.Module,
                period, ct);
            entitlements.RequireWithinLimit(Entitlements.AiMonthlyUnits, used + 1);

            var userPrompt = ComposeUserPrompt(workload,
                entitlements.Limit(Entitlements.AiMaxContextTokens));

            var estimatedTokens = AiUsage.EstimateTokens(workload.SystemPrompt)
                + AiUsage.EstimateTokens(userPrompt);
            entitlements.RequireWithinLimit(Entitlements.AiMaxContextTokens, estimatedTokens);

            var completion = await ExecuteWithFallbackAsync(workload, userPrompt,
                estimatedTokens, ct);

            await _usage.RecordAsync(user.OrganizationId, workload.Module, period, 1, ct);

            return completion;
        }

        private async Task<AiCompletion> ExecuteWithFallbackAsync(AiWorkload workload,
            string userPrompt, int estimatedTokens, CancellationToken ct) {
            Exception? lastFailure = null;
            var attempted = new HashSet<(string Provider, string Model)>();

            foreach (var tier in TierOrder.SkipWhile(t => t != workload.RequiredTier)) {
                var route = _router.Resolve(tier);

                if (!attempted.Add((route.Provider, route.Model))) {
                    continue;
                }

                if (!_clients.TryGetValue(route.Provider, out var client)) {
                    _logger.LogWarning("No AI client registered for provider {Provider}.",
                        route.Provider);
                    continue;
                }

                try {
                    var content = await client.CompleteAsync(route.Model,
                        workload.SystemPrompt, userPrompt, route.MaxOutputTokens, ct);

                    return new AiCompletion(content, route.Provider, route.Model, tier,
                        estimatedTokens);
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception exception) {
                    lastFailure = exception;
                    _logger.LogWarning(exception,
                        "AI provider {Provider} failed for capability {Capability}; " +
                        "trying the next tier down.",
                        route.Provider, workload.Capability);
                }
            }

            throw new AiUnavailableException(
                "The AI service is temporarily unavailable. Please try again shortly.",
                lastFailure);
        }

        private static string ComposeUserPrompt(AiWorkload workload, long contextTokenLimit) {
            if (workload.Context.Count == 0) {
                return workload.UserPrompt;
            }

            // Documents share the token budget left after the prompts; each is truncated to its
            // share so one oversized document cannot starve the rest or blow the limit.
            var promptTokens = AiUsage.EstimateTokens(workload.SystemPrompt)
                + AiUsage.EstimateTokens(workload.UserPrompt);
            var budgetTokens = Math.Max(1000, contextTokenLimit - promptTokens - 500);
            var perDocumentChars = (int)Math.Max(500,
                budgetTokens * 4 / workload.Context.Count);

            var builder = new StringBuilder(workload.UserPrompt);
            builder.AppendLine();

            foreach (var document in workload.Context) {
                builder.AppendLine();
                builder.AppendLine($"<document title=\"{document.Title}\">");
                builder.AppendLine(document.Content.Length <= perDocumentChars
                    ? document.Content
                    : document.Content[..perDocumentChars] + "\n[truncated]");
                builder.AppendLine("</document>");
            }

            return builder.ToString();
        }
    }
}
