using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ledgance.Shared.Infrastructure.Ai {
    /// <summary>
    /// The only path to an agentic provider. Every run requires the 'agentic' AI tier and is
    /// charged once, up front, at the capability's cost — a multi-step run is a single expensive
    /// operation, and paying for it before it starts means a run that exhausts the allowance
    /// half way through cannot happen. The loop itself is bounded by the workload's tool-step
    /// budget. Tool failures — including authorization denials from the nested pipeline — are
    /// reported back to the agent as tool results, never swallowed and never bypassed. Provider
    /// fallback goes down the tier chain on the first turn only; a provider that dies
    /// mid-conversation aborts the run.
    /// </summary>
    public sealed class AgentRunnerService : IAgentRunner {
        private static readonly string[] TierOrder =
            [AiTiers.Agentic, AiTiers.Reasoning, AiTiers.Advanced, AiTiers.Basic];

        private const int MaxToolResultChars = 6000;

        private readonly ICurrentUserAccessor _currentUser;
        private readonly IEntitlementService _entitlements;
        private readonly IAiUsageMeter _usage;
        private readonly IAiUsagePeriodResolver _periods;
        private readonly IAiOperationCosts _costs;
        private readonly IAiModelRouter _router;
        private readonly IReadOnlyDictionary<string, IAgentToolClient> _clients;
        private readonly ILogger<AgentRunnerService> _logger;

        public AgentRunnerService(ICurrentUserAccessor currentUser,
            IEntitlementService entitlements, IAiUsageMeter usage,
            IAiUsagePeriodResolver periods, IAiOperationCosts costs, IAiModelRouter router,
            IEnumerable<IAgentToolClient> agentClients, IEnumerable<IAiChatClient> chatClients,
            ILogger<AgentRunnerService> logger) {
            _currentUser = currentUser;
            _entitlements = entitlements;
            _usage = usage;
            _periods = periods;
            _costs = costs;
            _router = router;
            _logger = logger;

            var clients = agentClients.ToDictionary(client => client.Provider);

            foreach (var chatClient in chatClients) {
                if (!clients.ContainsKey(chatClient.Provider)) {
                    clients[chatClient.Provider] = new ChatAgentAdapter(chatClient);
                }
            }

            _clients = clients;
        }

        public async Task<AgentRunResult> RunAsync(AgentWorkload workload,
            CancellationToken ct) {
            var user = _currentUser.Require();
            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                workload.Module, ct);

            AiEntitlementGate.Require(entitlements, workload.Capability, AiTiers.Agentic,
                workload.RequiredReportScope, AiAnalysisScopes.Portfolio);

            var reservation = await AiUsageReservations.TakeAsync(_usage, _periods, _costs,
                entitlements, new AiUsageContext(user.OrganizationId, user.UserId,
                    workload.Module, workload.Capability, workload.ClientId,
                    workload.EngagementId),
                workload.Cost, ct);

            try {
                return await RunTurnsAsync(workload, reservation.Charge, ct);
            }
            catch {
                await AiUsageReservations.ReleaseAsync(_usage, user.OrganizationId,
                    reservation, _logger, ct);
                throw;
            }
        }

        private async Task<AgentRunResult> RunTurnsAsync(AgentWorkload workload,
            AiUsageCharge charge, CancellationToken ct) {
            var exchanges = new List<AgentExchange>();
            var steps = new List<AgentStep>();

            (IAgentToolClient Client, AiModelRoute Route, string Tier)? active = null;
            var turnsUsed = 0;

            while (true) {
                var exhausted = steps.Count >= workload.MaxToolSteps;
                var tools = exhausted ? [] : workload.Tools;
                var goal = exhausted
                    ? workload.Goal + "\n\nYou have used all available tool steps. Produce " +
                      "your final answer now from what you have gathered."
                    : workload.Goal;

                AgentTurn turn;

                if (active is null) {
                    (turn, active) = await FirstTurnWithFallbackAsync(workload, goal, tools,
                        exchanges, ct);
                }
                else {
                    turn = await active.Value.Client.NextTurnAsync(active.Value.Route.Model,
                        workload.SystemPrompt, goal, tools, exchanges,
                        active.Value.Route.MaxOutputTokens, ct);
                }

                turnsUsed++;

                if (turn.ToolCall is null || exhausted) {
                    return new AgentRunResult(
                        turn.FinalAnswer ?? "The agent did not produce an answer.",
                        steps, active.Value.Client.Provider, active.Value.Route.Model,
                        turnsUsed, charge);
                }

                var result = await ExecuteToolAsync(workload.Tools, turn.ToolCall, ct);
                exchanges.Add(new AgentExchange(turn.ToolCall, result));
                steps.Add(new AgentStep(turn.ToolCall.Tool, turn.ToolCall.ArgumentsJson,
                    result));
            }
        }

        private async Task<(AgentTurn, (IAgentToolClient, AiModelRoute, string))>
            FirstTurnWithFallbackAsync(AgentWorkload workload, string goal,
                IReadOnlyList<AgentTool> tools, IReadOnlyList<AgentExchange> exchanges,
                CancellationToken ct) {
            Exception? lastFailure = null;
            var attempted = new HashSet<(string, string)>();

            foreach (var tier in TierOrder) {
                var route = _router.Resolve(tier);

                if (!attempted.Add((route.Provider, route.Model))) {
                    continue;
                }

                if (!_clients.TryGetValue(route.Provider, out var client)) {
                    _logger.LogWarning("No agent client available for provider {Provider}.",
                        route.Provider);
                    continue;
                }

                try {
                    var turn = await client.NextTurnAsync(route.Model, workload.SystemPrompt,
                        goal, tools, exchanges, route.MaxOutputTokens, ct);

                    return (turn, (client, route, tier));
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception exception) {
                    lastFailure = exception;
                    _logger.LogWarning(exception,
                        "Agent provider {Provider} failed for capability {Capability}; " +
                        "trying the next tier down.",
                        route.Provider, workload.Capability);
                }
            }

            throw new AiUnavailableException(
                "The AI agent service is temporarily unavailable. Please try again shortly.",
                lastFailure);
        }

        private static async Task<string> ExecuteToolAsync(IReadOnlyList<AgentTool> tools,
            AgentToolCall call, CancellationToken ct) {
            var tool = tools.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, call.Tool, StringComparison.OrdinalIgnoreCase));

            if (tool is null) {
                return $"Error: '{call.Tool}' is not an available tool. Available tools: " +
                    string.Join(", ", tools.Select(candidate => candidate.Name)) + ".";
            }

            string result;

            try {
                using var arguments = JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);

                result = await tool.ExecuteAsync(arguments.RootElement.Clone(), ct);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (JsonException) {
                result = "Error: the tool arguments were not valid JSON.";
            }
            catch (ForbiddenException) {
                result = $"Access denied: you are not authorized to use '{tool.Name}'.";
            }
            catch (EntitlementException exception) {
                result = $"Not included in the subscription: {exception.Message}";
            }
            catch (DomainRuleException exception) {
                result = $"Rejected by a business rule: {exception.Message}";
            }
            catch (Exception exception) {
                result = $"The tool failed: {exception.Message}";
            }

            return result.Length <= MaxToolResultChars
                ? result
                : result[..MaxToolResultChars] + "\n[truncated]";
        }
    }
}
