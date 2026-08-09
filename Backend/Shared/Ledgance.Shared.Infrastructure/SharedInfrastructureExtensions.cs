using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Identity;
using Ledgance.Shared.Infrastructure.Subscriptions;
using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Client = Supabase.Client;
using SupabaseClientOptions = Supabase.SupabaseOptions;

namespace Ledgance.Shared.Infrastructure {
    public static class SharedInfrastructureExtensions {
        public static IServiceCollection AddLedganceSharedInfrastructure(
            this IServiceCollection services, IConfiguration configuration,
            Action<PermissionRegistry>? modulePermissions = null) {
            services.AddOptions<SupabaseSettings>()
                .Bind(configuration.GetSection(SupabaseSettings.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.Configure<SubscriptionSettings>(
                configuration.GetSection(SubscriptionSettings.SectionName));

            services.AddSingleton(provider => {
                var settings = provider.GetRequiredService<IOptions<SupabaseSettings>>().Value;

                return new Client(settings.Url, settings.ServiceRoleKey, new SupabaseClientOptions {
                    AutoConnectRealtime = false,
                    AutoRefreshToken = false
                });
            });

            services.AddHostedService<SupabaseClientInitializer>();

            services.AddSingleton(_ => {
                var registry = SharedPermissions.RegisterInto(new PermissionRegistry());
                modulePermissions?.Invoke(registry);

                return registry;
            });

            services.AddScoped<CurrentUserContext>();
            services.AddScoped<ICurrentUserAccessor>(provider =>
                provider.GetRequiredService<CurrentUserContext>());
            services.AddScoped<ICurrentUserInitializer>(provider =>
                provider.GetRequiredService<CurrentUserContext>());

            services.AddScoped<IOrganizationMembershipReader, OrganizationMembershipReader>();
            services.AddScoped<ISubscriptionReader, SupabaseSubscriptionReader>();
            services.AddScoped<IEntitlementService, EntitlementService>();

            services.AddScoped(typeof(SupabaseRepository<>));

            return services;
        }
    }

    /// <summary>
    /// Initialization is best-effort so a developer without Supabase credentials can still run
    /// the API; the client is usable for table access either way.
    /// </summary>
    internal sealed class SupabaseClientInitializer : IHostedService {
        private readonly Client _client;
        private readonly ILogger<SupabaseClientInitializer> _logger;

        public SupabaseClientInitializer(Client client,
            ILogger<SupabaseClientInitializer> logger) {
            _client = client;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            try {
                await _client.InitializeAsync();
            }
            catch (Exception exception) {
                _logger.LogWarning(exception,
                    "Supabase client initialization failed. Verify the Supabase configuration.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
