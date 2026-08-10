using Ledgance.Shared.Application.Ai;
using System.Text;
using System.Text.Json;

namespace Ledgance.Shared.Infrastructure.Ai {
    /// <summary>
    /// Drives an agent loop over a plain chat provider using a strict-JSON protocol, so agent
    /// runs can fall back down the provider chain when OpenClaw is unavailable. A reply that
    /// cannot be parsed as a decision is treated as the final answer rather than failing the
    /// run.
    /// </summary>
    internal sealed class ChatAgentAdapter : IAgentToolClient {
        private readonly IAiChatClient _chat;

        public ChatAgentAdapter(IAiChatClient chat) {
            _chat = chat;
        }

        public string Provider => _chat.Provider;

        public async Task<AgentTurn> NextTurnAsync(string model, string systemPrompt,
            string goal, IReadOnlyList<AgentTool> tools,
            IReadOnlyList<AgentExchange> exchanges, int maxOutputTokens,
            CancellationToken ct) {
            var content = await _chat.CompleteAsync(model,
                systemPrompt + "\n\n" + ProtocolInstruction(tools),
                ComposePrompt(goal, tools, exchanges), maxOutputTokens, ct);

            return Parse(content);
        }

        private static string ProtocolInstruction(IReadOnlyList<AgentTool> tools) =>
            tools.Count == 0
                ? "Reply with plain text only — no tools are available."
                : "You work step by step using tools. Reply with EXACTLY ONE JSON object " +
                  "and nothing else, in one of these two forms:\n" +
                  "{\"action\":\"call_tool\",\"tool\":\"<name>\",\"arguments\":{...}}\n" +
                  "{\"action\":\"final\",\"answer\":\"<your complete answer>\"}\n" +
                  "Call one tool at a time; give the final answer once you have enough " +
                  "information.";

        private static string ComposePrompt(string goal, IReadOnlyList<AgentTool> tools,
            IReadOnlyList<AgentExchange> exchanges) {
            var builder = new StringBuilder();
            builder.AppendLine("Goal:");
            builder.AppendLine(goal);

            if (tools.Count > 0) {
                builder.AppendLine();
                builder.AppendLine("Available tools:");

                foreach (var tool in tools) {
                    builder.AppendLine(
                        $"- {tool.Name}: {tool.Description} Parameters: {tool.ParametersSchema}");
                }
            }

            if (exchanges.Count > 0) {
                builder.AppendLine();
                builder.AppendLine("Tool calls so far:");

                foreach (var exchange in exchanges) {
                    builder.AppendLine(
                        $"<tool_call name=\"{exchange.Call.Tool}\" arguments='{exchange.Call.ArgumentsJson}'>");
                    builder.AppendLine(exchange.Result);
                    builder.AppendLine("</tool_call>");
                }
            }

            return builder.ToString();
        }

        private static AgentTurn Parse(string content) {
            var text = content.Trim();

            if (text.StartsWith("```")) {
                var firstBreak = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

                if (firstBreak >= 0 && lastFence > firstBreak) {
                    text = text[(firstBreak + 1)..lastFence].Trim();
                }
            }

            if (!text.StartsWith('{')) {
                return new AgentTurn(content, null);
            }

            try {
                using var payload = JsonDocument.Parse(text);
                var root = payload.RootElement;
                var action = root.TryGetProperty("action", out var actionProperty)
                    ? actionProperty.GetString()
                    : null;

                return action switch {
                    "call_tool" => new AgentTurn(null, new AgentToolCall(
                        root.GetProperty("tool").GetString() ?? string.Empty,
                        root.TryGetProperty("arguments", out var arguments)
                            ? arguments.GetRawText()
                            : "{}")),
                    "final" => new AgentTurn(
                        root.GetProperty("answer").GetString() ?? string.Empty, null),
                    _ => new AgentTurn(content, null)
                };
            }
            catch (JsonException) {
                return new AgentTurn(content, null);
            }
        }
    }
}
