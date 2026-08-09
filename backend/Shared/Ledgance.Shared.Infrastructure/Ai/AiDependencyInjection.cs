using Ledgance.Shared.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledgance.Shared.Infrastructure.Ai {
    public static class AiDependencyInjection {
        public static IServiceCollection AddLedganceAi(this IServiceCollection services,
            IConfiguration configuration) {
            services.Configure<AiSettings>(configuration.GetSection(AiSettings.SectionName));

            services.AddSingleton<IAiModelRouter, ConfiguredAiModelRouter>();

            services.AddHttpClient<OllamaChatClient>(client =>
                client.Timeout = TimeSpan.FromMinutes(3));
            services.AddHttpClient<OpenAiChatClient>(client =>
                client.Timeout = TimeSpan.FromMinutes(3));

            services.AddScoped<IAiChatClient>(provider =>
                provider.GetRequiredService<OllamaChatClient>());
            services.AddScoped<IAiChatClient>(provider =>
                provider.GetRequiredService<OpenAiChatClient>());
            services.AddScoped<IAiChatClient, AnthropicChatClient>();

            services.AddScoped<IAiUsageMeter, SupabaseAiUsageMeter>();
            services.AddScoped<IAiCompletionService, AiCompletionService>();

            return services;
        }
    }
}
