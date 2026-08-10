using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Constants = Supabase.Postgrest.Constants;

namespace Ledgance.Shared.Infrastructure.Ai {
    [Table("ai_usage")]
    public class AiUsageModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("module")]
        public string Module { get; set; } = string.Empty;

        [Column("period")]
        public string Period { get; set; } = string.Empty;

        [Column("units_used")]
        public long UnitsUsed { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    internal sealed class SupabaseAiUsageMeter : IAiUsageMeter {
        private readonly SupabaseRepository<AiUsageModel> _repository;

        public SupabaseAiUsageMeter(SupabaseRepository<AiUsageModel> repository) {
            _repository = repository;
        }

        public async Task<long> GetUsedAsync(Guid organizationId, ProductModule module,
            string period, CancellationToken ct) {
            var row = await FindAsync(module, period, ct);
            return row?.UnitsUsed ?? 0;
        }

        public async Task RecordAsync(Guid organizationId, ProductModule module,
            string period, long units, CancellationToken ct) {
            var row = await FindAsync(module, period, ct);

            if (row is null) {
                await _repository.InsertAsync(new AiUsageModel {
                    Id = Guid.NewGuid(),
                    Module = module.ToString(),
                    Period = period,
                    UnitsUsed = units,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
            }
            else {
                row.UnitsUsed += units;
                row.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(row, ct);
            }
        }

        private async Task<AiUsageModel?> FindAsync(ProductModule module, string period,
            CancellationToken ct) {
            var rows = await _repository.Query()
                .Filter("module", Constants.Operator.Equals, module.ToString())
                .Filter("period", Constants.Operator.Equals, period)
                .Limit(1)
                .Get(ct);

            return rows.Models.FirstOrDefault();
        }
    }
}
