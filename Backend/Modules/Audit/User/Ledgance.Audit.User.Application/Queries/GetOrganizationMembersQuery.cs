using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.User.Application.Queries {
    [RequiresPermission(SharedPermissions.MembersRead)]
    public class GetOrganizationMembersQuery : IQuery<Result<IEnumerable<OrganizationMemberRow>>> { }

    public class OrganizationMemberRow {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class GetOrganizationMembersQueryHandler
        : IRequestHandler<GetOrganizationMembersQuery, Result<IEnumerable<OrganizationMemberRow>>> {
        private readonly IOrganizationDirectory _directory;
        private readonly ICurrentUserAccessor _currentUser;

        public GetOrganizationMembersQueryHandler(IOrganizationDirectory directory,
            ICurrentUserAccessor currentUser) {
            _directory = directory;
            _currentUser = currentUser;
        }

        public async Task<Result<IEnumerable<OrganizationMemberRow>>> HandleAsync(
            GetOrganizationMembersQuery request, CancellationToken ct) {
            var members = await _directory.ListMembersAsync(
                _currentUser.RequireOrganizationId(), ct);

            return Result<IEnumerable<OrganizationMemberRow>>.Success(members
                .Select(member => new OrganizationMemberRow {
                    UserId = member.UserId,
                    DisplayName = member.DisplayName,
                    Email = member.Email,
                    Role = member.Role.ToString()
                }));
        }
    }
}
