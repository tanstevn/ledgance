namespace Ledgance.Shared.Application.Authorization {
    /// <summary>
    /// Opts a request out of the default-deny authorization pipeline.
    /// Only sign-in, sign-up and other pre-authentication flows may use this.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AllowAnonymousRequestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresPermissionAttribute : Attribute {
        public RequiresPermissionAttribute(string permission) {
            Permission = permission;
        }

        public string Permission { get; }
    }
}
