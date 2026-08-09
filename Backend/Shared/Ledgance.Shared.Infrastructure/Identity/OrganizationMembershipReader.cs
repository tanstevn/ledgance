using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Infrastructure.Supabase.Models;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Identity {
    public sealed record OrganizationMembership(Guid OrganizationId, OrganizationRole Role);

    public interface IOrganizationMembershipReader {
        Task<OrganizationMembership?> FindAsync(Guid userId, CancellationToken ct);
    }

    /// <summary>
    /// Reads membership with the service-role client because it runs before an organization
    /// context exists, so the tenant-scoped repository cannot be used here.
    /// </summary>
    internal sealed class OrganizationMembershipReader : IOrganizationMembershipReader {
        private readonly Client _client;

        public OrganizationMembershipReader(Client client) {
            _client = client;
        }

        public async Task<OrganizationMembership?> FindAsync(Guid userId, CancellationToken ct) {
            var memberships = await _client.From<OrganizationMemberModel>()
                .Filter("user_id", Constants.Operator.Equals, userId.ToString())
                .Order("is_default", Constants.Ordering.Descending)
                .Order("created_at", Constants.Ordering.Ascending)
                .Limit(1)
                .Get(ct);

            var membership = memberships.Models.FirstOrDefault();

            if (membership is null) {
                return null;
            }

            return new OrganizationMembership(membership.OrganizationId,
                ParseRole(membership.Role));
        }

        private static OrganizationRole ParseRole(string role) =>
            Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed)
                ? parsed
                : OrganizationRole.Viewer;
    }
}
