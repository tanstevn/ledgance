using Ledgance.Shared.Application.Identity;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Supabase {
    /// <summary>
    /// Tenant-scoped access to a Supabase table. Every query this returns is already filtered by
    /// the caller's organization when <typeparamref name="TModel"/> is <see cref="IOrganizationOwned"/>,
    /// which is the working guarantee of organization isolation; row-level security is the backstop.
    /// </summary>
    public class SupabaseRepository<TModel>
        where TModel : BaseModel, IEntityModel, new() {
        private static readonly bool IsTenantScoped =
            typeof(IOrganizationOwned).IsAssignableFrom(typeof(TModel));

        private readonly Client _client;
        private readonly ICurrentUserAccessor _currentUser;

        public SupabaseRepository(Client client, ICurrentUserAccessor currentUser) {
            _client = client;
            _currentUser = currentUser;
        }

        protected Guid OrganizationId => _currentUser.RequireOrganizationId();

        public IPostgrestTable<TModel> Query() {
            var table = _client.From<TModel>();

            return IsTenantScoped
                ? table.Filter(TenantColumns.OrganizationId,
                    Constants.Operator.Equals, OrganizationId.ToString())
                : table;
        }

        public async Task<TModel?> FindAsync(Guid id, CancellationToken ct) =>
            await Query()
                .Filter(TenantColumns.Id, Constants.Operator.Equals, id.ToString())
                .Single(ct);

        public async Task<TModel> GetAsync(Guid id, CancellationToken ct) =>
            await FindAsync(id, ct)
                ?? throw new InvalidOperationException(
                    $"{typeof(TModel).Name} '{id}' was not found.");

        public async Task<IReadOnlyList<TModel>> ListAsync(CancellationToken ct) =>
            (await Query().Get(ct)).Models;

        public async Task<long> CountAsync(CancellationToken ct) =>
            await Query().Count(Constants.CountType.Exact, ct);

        public async Task<TModel> InsertAsync(TModel model, CancellationToken ct) {
            TenantScope.Stamp(model, OrganizationId);

            var response = await _client.From<TModel>().Insert(model, cancellationToken: ct);

            return response.Model
                ?? throw new InvalidOperationException(
                    $"Insert of {typeof(TModel).Name} returned no row.");
        }

        public async Task<TModel> UpdateAsync(TModel model, CancellationToken ct) {
            TenantScope.Guard(model, OrganizationId);

            var response = await _client.From<TModel>().Update(model, cancellationToken: ct);

            return response.Model
                ?? throw new InvalidOperationException(
                    $"Update of {typeof(TModel).Name} '{model.Id}' returned no row.");
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct) =>
            await Query()
                .Filter(TenantColumns.Id, Constants.Operator.Equals, id.ToString())
                .Delete(cancellationToken: ct);
    }
}
