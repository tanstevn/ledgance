using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Shared.Infrastructure.Supabase {
    public interface IEntityModel {
        Guid Id { get; set; }
    }

    /// <summary>
    /// Marks a persistence model as tenant data. Every read and write through
    /// <see cref="SupabaseRepository{TModel}"/> is filtered by the caller's organization.
    /// </summary>
    public interface IOrganizationOwned {
        Guid OrganizationId { get; set; }
    }

    public static class TenantColumns {
        public const string OrganizationId = "organization_id";
        public const string Id = "id";
    }

    public static class TenantScope {
        public static void Stamp(object model, Guid organizationId) {
            if (model is IOrganizationOwned owned) {
                owned.OrganizationId = organizationId;
            }
        }

        public static void Guard(object model, Guid organizationId) {
            if (model is IOrganizationOwned owned && owned.OrganizationId != organizationId) {
                throw ForbiddenException.CrossOrganizationAccess();
            }
        }
    }
}
