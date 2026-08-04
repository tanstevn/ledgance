namespace Ledgance.Api {
    internal static class DependencyInjection {
        internal static void ConfigureConfiguration(IConfigurationManager config) {
            config.AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        }

        internal static void ConfigureServices(IServiceCollection services, IConfiguration config) {
            
        }

        internal static void ConfigureApplication(WebApplication app, IWebHostEnvironment env) {

        }
    }
}
