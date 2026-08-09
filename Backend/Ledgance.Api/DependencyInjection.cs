using Ledgance.Api.Middlewares;
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

            var connectionString = config
                .GetConnectionString("Neon")!;

            services.AddControllers();
            services.AddEndpointsApiExplorer();

            var assemblies = new[] {
                typeof(AccountingAIAnchor).Assembly,
                typeof(AccountingClientAnchor).Assembly,
                typeof(AccountingOrgAnchor).Assembly,
                typeof(AccountingUserAnchor).Assembly,
                typeof(AuditAIAnchor).Assembly,
                typeof(AuditClientAnchor).Assembly,
                typeof(AuditOrgAnchor).Assembly,
                typeof(AuditUserAnchor).Assembly
            };

            services.AddMediatorFromAssemblies(assemblies);
            services.AddValidatorsFromAssemblies(assemblies);

            services.AddCors(options => {
                options.AddDefaultPolicy(policy => {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });
        }

        [SuppressMessage("Usage", "ASP0014:Suggest using top level route registrations",
            Justification = "app.UseEndpoints(...): This is just to handle the " +
            "fallback logic when someone tries to search API routes randomly")]
        public static void ConfigureApplication(WebApplication app, IWebHostEnvironment env) {
            if (!env.IsEnvironment("Production")) {
                app.MapOpenApi();
                app.MapScalarApiReference();

                app.MapGet("/", () => Results.Redirect("/scalar/v1"))
                    .ExcludeFromDescription();
            }

            app.UseCors();

            // Add middleware here that "IS NOT" endpoint/route context reliant
            app.UseMiddleware<ExceptionHandlerMiddleware>();

            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                endpoints.MapFallback(context => {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;

                    return context.Response
                        .WriteAsJsonAsync(string.Empty);
                });
            });

            // Add middleware here that "IS" endpoint/route context reliant
        }
    }
}
