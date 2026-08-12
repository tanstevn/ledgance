using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Ledgance.Audit.AI.Infrastructure {
    public static class AuditAiInfrastructureExtensions {
        public static IServiceCollection AddAuditAiInfrastructure(
            this IServiceCollection services) {
            services.AddScoped<IGeneratedReportRepository, GeneratedReportRepository>();
            services.AddScoped<EngagementReadSet>();

            return services;
        }
    }
}
