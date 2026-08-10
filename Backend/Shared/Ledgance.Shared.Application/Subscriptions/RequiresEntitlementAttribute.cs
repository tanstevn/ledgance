namespace Ledgance.Shared.Application.Subscriptions {
    /// <summary>
    /// Declares a boolean capability the caller's plan must include. Numeric limits depend on
    /// current domain state and are therefore checked inside handlers via <see cref="EntitlementSet"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresEntitlementAttribute : Attribute {
        public RequiresEntitlementAttribute(ProductModule module, string capability) {
            Module = module;
            Capability = capability;
        }

        public ProductModule Module { get; }
        public string Capability { get; }
    }
}
