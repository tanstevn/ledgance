using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ledgance.Shared.Infrastructure.Ai {
    /// <summary>
    /// OpenClaw's native agent-turn protocol: the service receives the goal, the tool catalog
    /// and the exchanges so far, and answers with either the next tool call or the final
    /// answer. Tool EXECUTION never leaves this application — OpenClaw only chooses.
    /// </summary>
    internal sealed class OpenClawAgentClient : IAgentToolClient {
        private readonly HttpClient _http;

        public OpenClawAgentClient(HttpClient http, IOptions<AiSettings> settings) {
            _http = http;
            _http.BaseAddress = new Uri(settings.Value.OpenClaw.BaseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                    settings.Value.OpenClaw.ApiKey);
        }

        public string Provider => AiProviders.OpenClaw;

        public async Task<AgentTurn> NextTurnAsync(string model, string systemPrompt,
            string goal, IReadOnlyList<AgentTool> tools,
            IReadOnlyList<AgentExchange> exchanges, int maxOutputTokens,
            CancellationToken ct) {
            var response = await _http.PostAsJsonAsync("v1/agent/turns", new {
                model,
                system = systemPrompt,
                goal,
                max_output_tokens = maxOutputTokens,
                tools = tools.Select(tool => new {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = tool.ParametersSchema
                }),
                exchanges = exchanges.Select(exchange => new {
                    tool = exchange.Call.Tool,
                    arguments = exchange.Call.ArgumentsJson,
                    result = exchange.Result
                })
            }, ct);

            response.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = payload.RootElement;
            var type = root.GetProperty("type").GetString();

            return type switch {
                "tool_call" => new AgentTurn(null, new AgentToolCall(
                    root.GetProperty("tool").GetString() ?? string.Empty,
                    root.TryGetProperty("arguments", out var arguments)
                        ? arguments.GetRawText()
                        : "{}")),
                "final" => new AgentTurn(
                    root.GetProperty("content").GetString() ?? string.Empty, null),
                _ => throw new AiUnavailableException(
                    $"OpenClaw returned an unknown turn type '{type}'.")
            };
        }
    }
}
