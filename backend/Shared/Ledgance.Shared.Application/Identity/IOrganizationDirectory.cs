namespace Ledgance.Shared.Application.Identity {
    public sealed record OrganizationMemberInfo(
        Guid UserId,
        string DisplayName,
        string Email,
        OrganizationRole Role);

    public sealed record OrganizationInfo(
        string Name,
        IReadOnlyList<string> Products);

    public interface IOrganizationDirectory {
        Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct);

        Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail, string? product,
            CancellationToken ct);

        Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(Guid organizationId,
            CancellationToken ct);

        Task<OrganizationInfo?> GetOrganizationAsync(Guid organizationId,
            CancellationToken ct);

        Task AddProductAsync(Guid organizationId, string product, CancellationToken ct);

        Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId, Guid userId,
            CancellationToken ct);
    }
}
