using System.ComponentModel.DataAnnotations;

namespace Ledgance.Shared.Infrastructure.Supabase {
    public sealed class SupabaseSettings {
        public const string SectionName = "Supabase";

        [Required]
        public string Url { get; set; } = string.Empty;

        [Required]
        public string AnonKey { get; set; } = string.Empty;

        /// <summary>
        /// Server-only key that bypasses row-level security. Never send this to a browser.
        /// </summary>
        [Required]
        public string ServiceRoleKey { get; set; } = string.Empty;

        /// <summary>
        /// Symmetric signing secret for projects issuing HS256 access tokens. When empty,
        /// token validation falls back to the project's published JWKS document.
        /// </summary>
        public string JwtSecret { get; set; } = string.Empty;

        public string Issuer => $"{Url.TrimEnd('/')}/auth/v1";

        public string JwksUrl => $"{Issuer}/.well-known/jwks.json";

        public string Audience { get; set; } = "authenticated";
    }
}
