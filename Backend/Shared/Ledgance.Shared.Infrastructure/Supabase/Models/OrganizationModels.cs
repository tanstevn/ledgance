using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Ledgance.Shared.Infrastructure.Supabase.Models {
    [Table("organizations")]
    public class OrganizationModel : BaseModel, IEntityModel {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("organization_members")]
    public class OrganizationMemberModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("role")]
        public string Role { get; set; } = string.Empty;

        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("is_default")]
        public bool IsDefault { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("organization_subscriptions")]
    public class OrganizationSubscriptionModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("module")]
        public string Module { get; set; } = string.Empty;

        [Column("plan")]
        public string Plan { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("stripe_customer_id")]
        public string? StripeCustomerId { get; set; }

        [Column("stripe_subscription_id")]
        public string? StripeSubscriptionId { get; set; }

        [Column("current_period_end")]
        public DateTime? CurrentPeriodEnd { get; set; }

        /// <summary>
        /// Negotiated per-organization entitlement overrides, keyed by entitlement name.
        /// </summary>
        [Column("entitlement_overrides")]
        public Dictionary<string, string>? EntitlementOverrides { get; set; }
    }
}
