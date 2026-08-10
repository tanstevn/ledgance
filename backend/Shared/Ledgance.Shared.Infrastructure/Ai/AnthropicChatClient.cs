using Anthropic;
using Anthropic.Models.Messages;
using Ledgance.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using LedganceAi = Ledgance.Shared.Application.Ai;

namespace Ledgance.Shared.Infrastructure.Ai {
    internal sealed class AnthropicChatClient : LedganceAi.IAiChatClient {
        private readonly AnthropicClient _client;

        public AnthropicChatClient(IOptions<AiSettings> settings) {
            _client = new AnthropicClient {
                ApiKey = settings.Value.Anthropic.ApiKey
            };
        }

        public string Provider => AiProviders.Anthropic;

        public async Task<string> CompleteAsync(string model, string systemPrompt,
            string userPrompt, int maxOutputTokens, CancellationToken ct) {
            var response = await _client.Messages.Create(new MessageCreateParams {
                Model = model,
                MaxTokens = maxOutputTokens,
                System = systemPrompt,
                Messages = [new() { Role = Role.User, Content = userPrompt }]
            });

            var text = string.Concat(response.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(block => block.Text));

            return text.Length > 0
                ? text
                : throw new AiUnavailableException("Anthropic returned an empty response.");
        }
    }
}
