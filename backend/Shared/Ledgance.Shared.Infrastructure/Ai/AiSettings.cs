using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Infrastructure.Ai {
    public sealed class AiSettings {
        public const string SectionName = "Ai";

        public OllamaSettings Ollama { get; set; } = new();
        public OpenAiSettings OpenAI { get; set; } = new();
        public AnthropicSettings Anthropic { get; set; } = new();
        public OpenClawSettings OpenClaw { get; set; } = new();

        /// <summary>
        /// Tier → provider/model policy. Configuration overrides these defaults per tier, so
        /// swapping a model is an appsettings change, not a code change.
        /// </summary>
        public Dictionary<string, AiRouteSettings> Routing { get; set; } = [];

        /// <summary>
        /// Capability key → AI units it consumes, overriding what the module's capability
        /// catalogue declares. Retuning what an operation costs a customer is a settings change,
        /// and it stays independent of which provider happens to serve the tier.
        /// </summary>
        public Dictionary<string, long> OperationCosts { get; set; } = [];

        public sealed class OllamaSettings {
            public string BaseUrl { get; set; } = "http://localhost:11434";
        }

        public sealed class OpenAiSettings {
            public string BaseUrl { get; set; } = "https://api.openai.com";
            public string ApiKey { get; set; } = string.Empty;
        }

        public sealed class AnthropicSettings {
            public string ApiKey { get; set; } = string.Empty;
        }

        public sealed class OpenClawSettings {
            public string BaseUrl { get; set; } = "https://api.openclaw.ai";
            public string ApiKey { get; set; } = string.Empty;
        }

        public sealed class AiRouteSettings {
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public int MaxOutputTokens { get; set; } = 2048;
        }
    }

    public static class AiProviders {
        public const string Ollama = "Ollama";
        public const string OpenAI = "OpenAI";
        public const string Anthropic = "Anthropic";
        public const string OpenClaw = "OpenClaw";
    }

    public sealed class ConfiguredAiModelRouter : IAiModelRouter {
        private static readonly Dictionary<string, AiModelRoute> Defaults = new() {
            [AiTiers.Basic] = new AiModelRoute(AiProviders.Ollama, "llama3.1:8b", 2048),
            [AiTiers.Advanced] = new AiModelRoute(AiProviders.OpenAI, "gpt-4o", 4096),
            [AiTiers.Reasoning] = new AiModelRoute(AiProviders.Anthropic, "claude-opus-5", 8192),
            [AiTiers.Agentic] = new AiModelRoute(AiProviders.OpenClaw, "openclaw-agent-1", 8192)
        };

        private readonly AiSettings _settings;

        public ConfiguredAiModelRouter(Microsoft.Extensions.Options.IOptions<AiSettings> settings) {
            _settings = settings.Value;
        }

        public AiModelRoute Resolve(string tier) {
            if (_settings.Routing.TryGetValue(tier, out var configured)
                && !string.IsNullOrWhiteSpace(configured.Provider)
                && !string.IsNullOrWhiteSpace(configured.Model)) {
                return new AiModelRoute(configured.Provider, configured.Model,
                    configured.MaxOutputTokens);
            }

            return Defaults.TryGetValue(tier, out var route)
                ? route
                : Defaults[AiTiers.Basic];
        }
    }
}
