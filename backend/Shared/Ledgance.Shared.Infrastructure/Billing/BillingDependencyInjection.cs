using Ledgance.Shared.Application.Billing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledgance.Shared.Infrastructure.Billing {
    public static class BillingDependencyInjection {
        public static IServiceCollection AddLedganceBilling(this IServiceCollection services,
            IConfiguration configuration) {
            var settings = configuration
                .GetSection(StripeSettings.SectionName)
                .Get<StripeSettings>() ?? new StripeSettings();

            services.AddSingleton(settings);
            services.AddSingleton<IBillingUrls, ConfiguredBillingUrls>();
            services.AddSingleton<IBillingPriceCatalog, ConfiguredBillingPriceCatalog>();
            services.AddSingleton<IBillingPriceReader, StripePriceReader>();
            services.AddSingleton<IBillingGateway, StripeBillingGateway>();
            services.AddSingleton<IBillingWebhookVerifier, StripeWebhookVerifier>();

            services.AddScoped<ISubscriptionStore, SupabaseSubscriptionStore>();
            services.AddScoped<IProcessedEventStore, SupabaseProcessedEventStore>();

            return services;
        }
    }
}
