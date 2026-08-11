using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Ledgance.Shared.Infrastructure.Supabase.Models {
    [Table("organizations")]
    public class OrganizationModel : BaseModel, IEntityModel {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("products")]
        public List<string> Products { get; set; } = [];

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("organization_members")]
    public class OrganizationMemberModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
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
        [PrimaryKey("id", true)]
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

        [Column("cancel_at_period_end")]
        public bool CancelAtPeriodEnd { get; set; }

        /// <summary>
        /// When the provider event behind this row was raised, so a webhook delivered out of
        /// order can be recognised as stale.
        /// </summary>
        [Column("last_event_at")]
        public DateTime? LastEventAt { get; set; }

        /// <summary>
        /// Negotiated per-organization entitlement overrides, keyed by entitlement name. Never
        /// null: the column is NOT NULL, and an insert sends every property, so a null here
        /// would be written as null rather than falling back to the column default.
        /// </summary>
        [Column("entitlement_overrides")]
        public Dictionary<string, string> EntitlementOverrides { get; set; } = [];
    }

    /// <summary>
    /// One row per provider event already applied. Not organization-owned: it is written from
    /// the webhook path, which has no user context, and holds no tenant data.
    /// </summary>
    [Table("billing_events")]
    public class BillingEventModel : BaseModel, IEntityModel {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("event_id")]
        public string EventId { get; set; } = string.Empty;

        [Column("event_type")]
        public string EventType { get; set; } = string.Empty;

        [Column("received_at")]
        public DateTime ReceivedAt { get; set; }
    }
}
