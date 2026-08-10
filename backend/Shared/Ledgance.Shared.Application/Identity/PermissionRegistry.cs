namespace Ledgance.Shared.Application.Identity {
    /// <summary>
    /// Single startup-populated grant table. Modules contribute their own permissions here
    /// so that no role-to-permission logic is duplicated inside feature code.
    /// </summary>
    public sealed class PermissionRegistry {
        private static readonly IReadOnlySet<string> None = new HashSet<string>();

        private readonly Dictionary<OrganizationRole, HashSet<string>> _grants = [];

        public PermissionRegistry Grant(string permission, params OrganizationRole[] roles) {
            ArgumentException.ThrowIfNullOrWhiteSpace(permission);

            foreach (var role in roles) {
                if (!_grants.TryGetValue(role, out var permissions)) {
                    permissions = [];
                    _grants[role] = permissions;
                }

                permissions.Add(permission);
            }

            return this;
        }

        public PermissionRegistry GrantFrom(string permission, OrganizationRole minimumRole) =>
            Grant(permission, Enum.GetValues<OrganizationRole>()
                .Where(role => role >= minimumRole)
                .ToArray());

        public IReadOnlySet<string> For(OrganizationRole role) =>
            _grants.TryGetValue(role, out var permissions)
                ? permissions
                : None;
    }
}
