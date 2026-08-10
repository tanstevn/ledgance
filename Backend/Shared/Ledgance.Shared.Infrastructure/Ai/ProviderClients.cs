using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ledgance.Shared.Infrastructure.Ai {
    internal sealed class OllamaChatClient : IAiChatClient {
        private readonly HttpClient _http;

        public OllamaChatClient(HttpClient http, IOptions<AiSettings> settings) {
            _http = http;
            _http.BaseAddress = new Uri(settings.Value.Ollama.BaseUrl.TrimEnd('/') + "/");
        }

        public string Provider => AiProviders.Ollama;

        public async Task<string> CompleteAsync(string model, string systemPrompt,
            string userPrompt, int maxOutputTokens, CancellationToken ct) {
            var response = await _http.PostAsJsonAsync("api/chat", new {
                model,
                stream = false,
                options = new { num_predict = maxOutputTokens },
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            }, ct);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct);

            return payload?.Message?.Content
                ?? throw new AiUnavailableException("Ollama returned an empty response.");
        }

        private sealed class OllamaResponse {
            [JsonPropertyName("message")]
            public OllamaMessage? Message { get; set; }
        }

        private sealed class OllamaMessage {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }

    internal sealed class OpenAiChatClient : IAiChatClient {
        private readonly HttpClient _http;

        public OpenAiChatClient(HttpClient http, IOptions<AiSettings> settings) {
            _http = http;
            _http.BaseAddress = new Uri(settings.Value.OpenAI.BaseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                    settings.Value.OpenAI.ApiKey);
        }

        public string Provider => AiProviders.OpenAI;

        public async Task<string> CompleteAsync(string model, string systemPrompt,
            string userPrompt, int maxOutputTokens, CancellationToken ct) {
            var response = await _http.PostAsJsonAsync("v1/chat/completions", new {
                model,
                max_tokens = maxOutputTokens,
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            }, ct);

            response.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            var content = payload.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content
                ?? throw new AiUnavailableException("OpenAI returned an empty response.");
        }
    }
}
