using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.Extensions.DependencyInjection;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Ledgance.Integration.AccountingContext {
    [Table("integration_accounting_links")]
    public class AccountingLinkModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("updated_by")]
        public Guid UpdatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    internal sealed class SupabaseAccountingLinkStore : IAccountingLinkStore {
        private readonly SupabaseRepository<AccountingLinkModel> _repository;
        private readonly ICurrentUserAccessor _currentUser;

        public SupabaseAccountingLinkStore(SupabaseRepository<AccountingLinkModel> repository,
            ICurrentUserAccessor currentUser) {
            _repository = repository;
            _currentUser = currentUser;
        }

        public async Task<bool> IsEnabledAsync(CancellationToken ct) {
            var rows = await _repository.ListAsync(ct);
            return rows.FirstOrDefault()?.IsEnabled ?? false;
        }

        public async Task SetEnabledAsync(bool enabled, CancellationToken ct) {
            var rows = await _repository.ListAsync(ct);
            var existing = rows.FirstOrDefault();

            if (existing is null) {
                await _repository.InsertAsync(new AccountingLinkModel {
                    Id = Guid.NewGuid(),
                    IsEnabled = enabled,
                    UpdatedBy = _currentUser.Require().UserId,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
                return;
            }

            existing.IsEnabled = enabled;
            existing.UpdatedBy = _currentUser.Require().UserId;
            existing.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(existing, ct);
        }
    }

    public static class AccountingContextIntegrationExtensions {
        public static IServiceCollection AddAccountingContextIntegration(
            this IServiceCollection services) {
            services.AddScoped<IAccountingLinkStore, SupabaseAccountingLinkStore>();
            services.AddScoped<ILinkedAccountingSource, LinkedAccountingSourceAdapter>();

            return services;
        }
    }
}
