using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.Extensions.DependencyInjection;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Constants = Supabase.Postgrest.Constants;
using DomainClient = Ledgance.Audit.Client.Domain.AuditClient;

namespace Ledgance.Audit.Client.Infrastructure {
    [Table("audit_clients")]
    public class AuditClientModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("industry")]
        public string Industry { get; set; } = string.Empty;

        [Column("contact_name")]
        public string ContactName { get; set; } = string.Empty;

        [Column("contact_email")]
        public string ContactEmail { get; set; } = string.Empty;

        [Column("contact_phone")]
        public string ContactPhone { get; set; } = string.Empty;

        [Column("website")]
        public string? Website { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    internal sealed class ClientRepository : IClientRepository {
        private readonly SupabaseRepository<AuditClientModel> _repository;

        public ClientRepository(SupabaseRepository<AuditClientModel> repository) {
            _repository = repository;
        }

        public async Task<DomainClient?> FindAsync(Guid id, CancellationToken ct) {
            var model = await _repository.FindAsync(id, ct);
            return model is null ? null : ToDomain(model);
        }

        public async Task<IReadOnlyList<DomainClient>> ListAsync(bool includeArchived,
            CancellationToken ct) {
            var query = _repository.Query();

            if (!includeArchived) {
                query = query.Filter("is_archived", Constants.Operator.Equals, "false");
            }

            var rows = await query.Order("name", Constants.Ordering.Ascending).Get(ct);
            return rows.Models.Select(ToDomain).ToList();
        }

        public async Task<ClientPage> ListPageAsync(int page, int pageSize, string? search,
            CancellationToken ct) {
            var query = _repository.Query();
            var countQuery = _repository.Query();

            if (!string.IsNullOrWhiteSpace(search)) {
                var pattern = $"%{search.Trim()}%";
                query = query.Filter("name", Constants.Operator.ILike, pattern);
                countQuery = countQuery.Filter("name", Constants.Operator.ILike, pattern);
            }

            var from = (page - 1) * pageSize;

            var rows = await query
                .Order("name", Constants.Ordering.Ascending)
                .Range(from, from + pageSize - 1)
                .Get(ct);

            var total = await countQuery.Count(Constants.CountType.Exact, ct);

            return new ClientPage(rows.Models.Select(ToDomain).ToList(), total);
        }

        public async Task<long> CountActiveAsync(CancellationToken ct) =>
            await _repository.Query()
                .Filter("is_archived", Constants.Operator.Equals, "false")
                .Count(Constants.CountType.Exact, ct);

        public async Task<DomainClient> AddAsync(DomainClient client, CancellationToken ct) {
            await _repository.InsertAsync(ToModel(client), ct);
            return client;
        }

        public async Task UpdateAsync(DomainClient client, CancellationToken ct) {
            var existing = await _repository.GetAsync(client.Id, ct);
            var model = ToModel(client);
            model.OrganizationId = existing.OrganizationId;

            await _repository.UpdateAsync(model, ct);
        }

        private static DomainClient ToDomain(AuditClientModel model) =>
            DomainClient.Restore(model.Id, model.Name, model.Industry, model.ContactName,
                model.ContactEmail, model.ContactPhone, model.Website, model.Address,
                model.IsArchived, model.CreatedAt);

        private static AuditClientModel ToModel(DomainClient client) =>
            new() {
                Id = client.Id,
                Name = client.Name,
                Industry = client.Industry,
                ContactName = client.ContactName,
                ContactEmail = client.ContactEmail,
                ContactPhone = client.ContactPhone,
                Website = client.Website,
                Address = client.Address,
                IsArchived = client.IsArchived,
                CreatedAt = client.CreatedAt
            };
    }

    public static class ClientInfrastructureExtensions {
        public static IServiceCollection AddAuditClientInfrastructure(this IServiceCollection services) {
            services.AddScoped<IClientRepository, ClientRepository>();
            return services;
        }
    }
}
