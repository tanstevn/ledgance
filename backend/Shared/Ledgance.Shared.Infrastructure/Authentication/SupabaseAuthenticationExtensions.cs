using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                        // Projects issuing asymmetric access tokens publish their keys instead
                        // of sharing a symmetric secret.
                        options.Authority = settings.Issuer;
                        options.MetadataAddress = settings.JwksUrl;
                    }
                });

            services.AddAuthorization(options =>
                options.FallbackPolicy = options.DefaultPolicy);

            return services;
        }
    }
}
