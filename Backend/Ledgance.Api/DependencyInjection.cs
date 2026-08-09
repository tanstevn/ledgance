using Ledgance.Api.Middlewares;
using Ledgance.Shared.Infrastructure;
using Ledgance.Shared.Infrastructure.Authentication;
using Ledgance.Shared.Infrastructure.Identity;
using Ledgance.Shared.Infrastructure.Mediator;
using Scalar.AspNetCore;
using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using AccountingAIAnchor = Ledgance.Accounting.AI.Application.MediatorAnchor;
using AccountingClientAnchor = Ledgance.Accounting.Client.Application.MediatorAnchor;
using AccountingOrgAnchor = Ledgance.Accounting.Organization.Application.MediatorAnchor;
using AccountingUserAnchor = Ledgance.Accounting.User.Application.MediatorAnchor;
using AuditAIAnchor = Ledgance.Audit.AI.Application.MediatorAnchor;
using AuditClientAnchor = Ledgance.Audit.Client.Application.MediatorAnchor;
using AuditOrgAnchor = Ledgance.Audit.Organization.Application.MediatorAnchor;
using AuditUserAnchor = Ledgance.Audit.User.Application.MediatorAnchor;

namespace Ledgance.Api {
    public static class DependencyInjection {
        private const string CorsPolicyName = "LedganceClients";

        public static void ConfigureConfiguration(IConfigurationManager config) {
            config.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        }

        public static void ConfigureServices(IServiceCollection services, IConfiguration config) {
            services.AddOpenApi(options => {
                options.AddDocumentTransformer((document, context, _) => {
                    document.Info = new() {
                        Title = "Ledgance API documentation",
                        Version = "v1"
                    };

                    return Task.CompletedTask;
                });
            });

            services.AddControllers();
            services.AddEndpointsApiExplorer();

            var moduleAssemblies = new[] {
                typeof(AccountingAIAnchor).Assembly,
                typeof(AccountingClientAnchor).Assembly,
                typeof(AccountingOrgAnchor).Assembly,
                typeof(AccountingUserAnchor).Assembly,
                typeof(AuditAIAnchor).Assembly,
                typeof(AuditClientAnchor).Assembly,
                typeof(AuditOrgAnchor).Assembly,
                typeof(AuditUserAnchor).Assembly
            };

            // The shared assembly carries the cross-cutting pipeline behaviors, so it has to be
            // part of the mediator scan alongside the modules that carry the handlers.
            services.AddMediatorFromAssemblies([
                .. moduleAssemblies,
                typeof(SharedInfrastructureExtensions).Assembly
            ]);

            services.AddValidatorsFromAssemblies(moduleAssemblies);

            services.AddLedganceSharedInfrastructure(config);
            services.AddSupabaseAuthentication(config);

            var allowedOrigins = config
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            services.AddCors(options => {
                options.AddPolicy(CorsPolicyName, policy => {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
        }

        [SuppressMessage("Usage", "ASP0014:Suggest using top level route registrations",
            Justification = "app.UseEndpoints(...): This is just to handle the " +
            "fallback logic when someone tries to search API routes randomly")]
        public static void ConfigureApplication(WebApplication app, IWebHostEnvironment env) {
            app.UseCors(CorsPolicyName);

            // Add middleware here that "IS NOT" endpoint/route context reliant
            app.UseMiddleware<ExceptionHandlerMiddleware>();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Add middleware here that "IS" endpoint/route context reliant
            app.UseMiddleware<CurrentUserMiddleware>();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();

                if (!env.IsEnvironment("Production")) {
                    endpoints.MapOpenApi().AllowAnonymous();
                    endpoints.MapScalarApiReference().AllowAnonymous();

                    endpoints.MapGet("/", () => Results.Redirect("/scalar/v1"))
                        .AllowAnonymous()
                        .ExcludeFromDescription();
                }

                endpoints.MapFallback(context => {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;

                    return context.Response
                        .WriteAsJsonAsync(string.Empty);
                }).AllowAnonymous();
            });
        }
    }
}
