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
            services.AddHttpClient<OpenClawAgentClient>(client =>
                client.Timeout = TimeSpan.FromMinutes(5));

            services.AddScoped<IAiChatClient>(provider =>
                provider.GetRequiredService<OllamaChatClient>());
            services.AddScoped<IAiChatClient>(provider =>
                provider.GetRequiredService<OpenAiChatClient>());
            services.AddScoped<IAiChatClient, AnthropicChatClient>();

            services.AddScoped<IAgentToolClient>(provider =>
                provider.GetRequiredService<OpenClawAgentClient>());

            services.AddSingleton<IAiOperationCosts, ConfiguredAiOperationCosts>();
            services.AddScoped<IAiUsageMeter, SupabaseAiUsageMeter>();
            services.AddScoped<IAiUsagePeriodResolver, SubscriptionAiUsagePeriodResolver>();
            services.AddScoped<IAiCompletionService, AiCompletionService>();
            services.AddScoped<IAgentRunner, AgentRunnerService>();

            return services;
        }
    }
}
