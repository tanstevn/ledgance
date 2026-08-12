using Ledgance.Shared.Application.Subscriptions;
using System.Text.Json;

namespace Ledgance.Shared.Application.Ai {
    /// <summary>
    /// A capability the agent may invoke. The executor is the ONLY way an agent touches the
    /// application — features build it to dispatch a whitelisted mediator request, so every
    /// invocation runs the full pipeline (authorization, entitlements, validation) with the
    /// calling user's identity. Agents never see a repository or the database.
    /// </summary>
    public sealed record AgentTool(
        string Name,
        string Description,
        string ParametersSchema,
        Func<JsonElement, CancellationToken, Task<string>> ExecuteAsync);

    public sealed record AgentToolCall(string Tool, string ArgumentsJson);

    /// <summary>
    /// One provider decision: either a tool to invoke next or the final answer, never both.
    /// </summary>
    public sealed record AgentTurn(string? FinalAnswer, AgentToolCall? ToolCall);

    public sealed record AgentExchange(AgentToolCall Call, string Result);

    public sealed record AgentStep(string Tool, string ArgumentsJson, string Result);

    public sealed record AgentWorkload(
        ProductModule Module,
        string Capability,
        string Goal,
        string SystemPrompt,
        IReadOnlyList<AgentTool> Tools,
        int MaxToolSteps = 8,
        string RequiredReportScope = AiReportScopes.None,
        long Cost = 1,
        Guid? ClientId = null,
        Guid? EngagementId = null);

    public sealed record AgentRunResult(
        string Answer,
        IReadOnlyList<AgentStep> Steps,
        string Provider,
        string Model,
        int TurnsUsed,
        AiUsageCharge? Usage = null);

    /// <summary>
    /// A provider that can drive an agent loop: given the goal, the tool catalog and the
    /// exchanges so far, it returns the next turn. OpenClaw implements this natively; chat
    /// providers are adapted onto it for fallback.
    /// </summary>
    public interface IAgentToolClient {
        string Provider { get; }

        Task<AgentTurn> NextTurnAsync(string model, string systemPrompt, string goal,
            IReadOnlyList<AgentTool> tools, IReadOnlyList<AgentExchange> exchanges,
            int maxOutputTokens, CancellationToken ct);
    }

    /// <summary>
    /// The single entry point features use to run an agent. Implementations must enforce
    /// authorization, the agentic entitlement tier, usage accounting and provider routing —
    /// features never talk to a provider.
    /// </summary>
    public interface IAgentRunner {
        Task<AgentRunResult> RunAsync(AgentWorkload workload, CancellationToken ct);
    }
}
