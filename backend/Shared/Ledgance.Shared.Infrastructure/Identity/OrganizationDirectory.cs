using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Infrastructure.Supabase.Models;
using System.Text.RegularExpressions;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Identity {
    /// <summary>
    /// Uses the service-role client because provisioning and membership lookups legitimately run
    /// before an organization context exists.
    /// </summary>
    internal sealed class OrganizationDirectory : IOrganizationDirectory {
        private readonly Client _client;

        public OrganizationDirectory(Client client) {
            _client = client;
        }

        public async Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) {
            var count = await _client.From<OrganizationMemberModel>()
                .Filter("user_id", Constants.Operator.Equals, userId.ToString())
                .Count(Constants.CountType.Exact, ct);

            return count > 0;
        }

        public async Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail, CancellationToken ct) {
            var organization = new OrganizationModel {
                Id = Guid.NewGuid(),
                Name = organizationName,
                Slug = BuildSlug(organizationName),
                CreatedAt = DateTime.UtcNow
            };

            await _client.From<OrganizationModel>().Insert(organization, cancellationToken: ct);

            await _client.From<OrganizationMemberModel>().Insert(new OrganizationMemberModel {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                UserId = ownerUserId,
                Role = nameof(OrganizationRole.Owner),
                DisplayName = ownerDisplayName,
                Email = ownerEmail,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken: ct);

            return organization.Id;
        }

        public async Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(
            Guid organizationId, CancellationToken ct) {
            var members = await _client.From<OrganizationMemberModel>()
                .Filter("organization_id", Constants.Operator.Equals, organizationId.ToString())
                .Order("created_at", Constants.Ordering.Ascending)
                .Get(ct);

            return members.Models
                .Select(ToInfo)
                .ToList();
        }

        public async Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId,
            Guid userId, CancellationToken ct) {
            var members = await _client.From<OrganizationMemberModel>()
                .Filter("organization_id", Constants.Operator.Equals, organizationId.ToString())
                .Filter("user_id", Constants.Operator.Equals, userId.ToString())
                .Limit(1)
                .Get(ct);

            var member = members.Models.FirstOrDefault();
            return member is null ? null : ToInfo(member);
        }

        private static OrganizationMemberInfo ToInfo(OrganizationMemberModel model) =>
            new(model.UserId, model.DisplayName, model.Email,
                Enum.TryParse<OrganizationRole>(model.Role, ignoreCase: true, out var role)
                    ? role
                    : OrganizationRole.Viewer);

        private static string BuildSlug(string name) {
            var normalized = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-")
                .Trim('-');

            if (normalized.Length > 40) {
                normalized = normalized[..40].Trim('-');
            }

            if (normalized.Length == 0) {
                normalized = "org";
            }

            return $"{normalized}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}
