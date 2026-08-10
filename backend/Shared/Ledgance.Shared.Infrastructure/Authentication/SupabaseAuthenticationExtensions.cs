using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ledgance.Shared.Infrastructure.Authentication {
    public static class SupabaseAuthenticationExtensions {
        public static IServiceCollection AddSupabaseAuthentication(
            this IServiceCollection services, IConfiguration configuration) {
            var settings = configuration
                .GetSection(SupabaseSettings.SectionName)
                .Get<SupabaseSettings>() ?? new SupabaseSettings();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => {
                    options.MapInboundClaims = false;

                    // Token contents are never logged — only why validation failed, so a
                    // misconfigured secret or issuer is diagnosable from the console.
                    options.Events = new JwtBearerEvents {
                        OnAuthenticationFailed = context => {
                            context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("Ledgance.Auth")
                                .LogWarning(
                                    "Bearer token validation failed: {Reason}",
                                    context.Exception.Message);
                            return Task.CompletedTask;
                        }
                    };

                    options.TokenValidationParameters = new TokenValidationParameters {
                        ValidateIssuer = true,
                        ValidIssuer = settings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = settings.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "sub",
                        RoleClaimType = "role"
                    };

                    if (!string.IsNullOrWhiteSpace(settings.JwtSecret)) {
                        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        options.TokenValidationParameters.IssuerSigningKey =
                            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret));
                    }
                    else {
                        // Projects issuing asymmetric access tokens publish their keys as a
                        // JWKS document rather than sharing a symmetric secret. Supabase Auth
                        // has no OIDC discovery endpoint, so the keys are resolved from the
                        // JWKS URL directly, fetched once and cached for the process lifetime —
                        // restart the API after rotating the project's signing keys.
                        var jwks = new Lazy<JsonWebKeySet>(() => {
                            using var http = new HttpClient();
                            var json = http.GetStringAsync(settings.JwksUrl)
                                .GetAwaiter().GetResult();
                            return new JsonWebKeySet(json);
                        });

                        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        options.TokenValidationParameters.IssuerSigningKeyResolver =
                            (_, _, _, _) => jwks.Value.GetSigningKeys();
                    }
                });

            services.AddAuthorization(options =>
                options.FallbackPolicy = options.DefaultPolicy);

            return services;
        }
    }
}
