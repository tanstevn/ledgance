namespace Ledgance.Shared.Application.Identity {
    public sealed class CurrentUser {
        public CurrentUser(Guid userId, string email, Guid organizationId,
            OrganizationRole role, IReadOnlySet<string> permissions) {
            UserId = userId;
            Email = email;
            OrganizationId = organizationId;
            Role = role;
            Permissions = permissions;
        }

        public Guid UserId { get; }
        public string Email { get; }
        public Guid OrganizationId { get; }
        public OrganizationRole Role { get; }
        public IReadOnlySet<string> Permissions { get; }

        public bool HasPermission(string permission) =>
            Permissions.Contains(permission);
    }
}
