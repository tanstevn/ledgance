namespace Ledgance.Shared.Application.Subscriptions {
    public enum ProductModule {
        Audit,
        Accounting
    }

    /// <summary>
    /// Stable internal plan identifiers. Display names and prices live outside the domain
    /// (presentation metadata and the payment provider respectively), so renaming or repricing
    /// a plan never touches business logic. A stored value that no longer maps to a member
    /// resolves to <see cref="Free"/>, which fails closed.
    /// </summary>
    public enum PlanCode {
        Free,
        AuditMicro,
        AuditMicroGrowth,
        AuditSmall,
        AuditMedium,
        AuditMediumGrowth,
        AuditEnterprise,
        AccountingSolo,
        AccountingTeam,
        AccountingProfessional,
        AccountingEnterprise
    }
}
