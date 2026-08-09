namespace Ledgance.Shared.Application.Exceptions {
    public class UnauthenticatedException : Exception {
        public UnauthenticatedException()
            : base("Authentication is required.") { }

        public UnauthenticatedException(string message)
            : base(message) { }
    }

    public class ForbiddenException : Exception {
        public ForbiddenException(string message)
            : base(message) { }

        public static ForbiddenException MissingPermission(string permission) =>
            new($"Missing required permission '{permission}'.");

        public static ForbiddenException CrossOrganizationAccess() =>
            new("The requested resource belongs to another organization.");
    }

    /// <summary>
    /// A domain invariant or workflow rule rejected the operation. Surfaced as HTTP 409 so the
    /// client can distinguish "the state does not allow this" from validation or permission errors.
    /// </summary>
    public class DomainRuleException : Exception {
        public DomainRuleException(string message)
            : base(message) { }
    }

    /// <summary>
    /// Signals that the caller's subscription does not allow the operation.
    /// Surfaced as HTTP 402 so the client can distinguish "upgrade required"
    /// from "not permitted for this role".
    /// </summary>
    public class EntitlementException : Exception {
        public EntitlementException(string message)
            : base(message) { }

        public static EntitlementException NotIncluded(string entitlement) =>
            new($"Your current plan does not include '{entitlement}'.");

        public static EntitlementException LimitReached(string entitlement, long limit) =>
            new($"Your current plan allows up to {limit} for '{entitlement}'.");
    }
}
