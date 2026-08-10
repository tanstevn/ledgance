using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Infrastructure.Supabase;
using Microsoft.Extensions.DependencyInjection;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Constants = Supabase.Postgrest.Constants;
using SupabaseClient = Supabase.Client;

namespace Ledgance.Audit.Engagement.Infrastructure {
    /// <summary>
    /// Read-only projection of the Client feature's table, scoped to the caller's organization.
    /// The Engagement feature never mutates client rows.
    /// </summary>
    [Table("audit_clients")]
    public class ClientRefModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("is_archived")]
        public bool IsArchived { get; set; }
    }

    internal sealed class ClientLookup : IClientLookup {
        private readonly SupabaseRepository<ClientRefModel> _repository;

        public ClientLookup(SupabaseRepository<ClientRefModel> repository) {
            _repository = repository;
        }

        public async Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken ct) {
            var model = await _repository.FindAsync(clientId, ct);
            return model is not null && !model.IsArchived;
        }

        public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
            IEnumerable<Guid> clientIds, CancellationToken ct) {
            var ids = clientIds.Distinct().ToList();

            if (ids.Count == 0) {
                return new Dictionary<Guid, string>();
            }

            var rows = await _repository.Query()
                .Filter("id", Constants.Operator.In, ids.Select(id => id.ToString()).ToList())
                .Get(ct);

            return rows.Models.ToDictionary(model => model.Id, model => model.Name);
        }
    }

    internal sealed class ClientEngagementCounter : IClientEngagementCounter {
        private readonly SupabaseRepository<EngagementModel> _repository;

        public ClientEngagementCounter(SupabaseRepository<EngagementModel> repository) {
            _repository = repository;
        }

        public async Task<int> CountActiveEngagementsAsync(Guid clientId, CancellationToken ct) =>
            (int)await _repository.Query()
                .Filter("client_id", Constants.Operator.Equals, clientId.ToString())
                .Filter("status", Constants.Operator.NotEqual,
                    nameof(EngagementStatus.Completed))
                .Count(Constants.CountType.Exact, ct);
    }

    internal sealed class SupabaseEvidenceFileStore : IEvidenceFileStore {
        public const string BucketName = "audit-evidence";

        private readonly SupabaseClient _client;

        public SupabaseEvidenceFileStore(SupabaseClient client) {
            _client = client;
        }

        public async Task<string> UploadAsync(Guid engagementId, Guid evidenceId, int version,
            string fileName, byte[] content, string contentType, CancellationToken ct) {
            var path = $"{engagementId}/{evidenceId}/v{version}/{fileName}";

            await _client.Storage
                .From(BucketName)
                .Upload(content, path, new global::Supabase.Storage.FileOptions {
                    ContentType = string.IsNullOrWhiteSpace(contentType)
                        ? "application/octet-stream"
                        : contentType,
                    Upsert = true
                });

            return path;
        }

        public async Task<string> CreateDownloadUrlAsync(string storagePath,
            TimeSpan lifetime, CancellationToken ct) =>
            await _client.Storage
                .From(BucketName)
                .CreateSignedUrl(storagePath, (int)lifetime.TotalSeconds);
    }

    public static class EngagementInfrastructureExtensions {
        public static IServiceCollection AddAuditEngagementInfrastructure(
            this IServiceCollection services) {
            services.AddScoped<IEngagementRepository, EngagementRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<IRiskRepository, RiskRepository>();
            services.AddScoped<IProcedureRepository, ProcedureRepository>();
            services.AddScoped<IWorkingPaperRepository, WorkingPaperRepository>();
            services.AddScoped<IEvidenceRepository, EvidenceRepository>();
            services.AddScoped<IFindingRepository, FindingRepository>();
            services.AddScoped<IReportRepository, ReportRepository>();
            services.AddScoped<ITrialBalanceRepository, TrialBalanceRepository>();
            services.AddScoped<IEngagementProgressReader, EngagementProgressReader>();
            services.AddScoped<IEngagementAccessGuard, EngagementAccessGuard>();
            services.AddScoped<IClientLookup, ClientLookup>();
            services.AddScoped<IClientEngagementCounter, ClientEngagementCounter>();
            services.AddScoped<IEvidenceFileStore, SupabaseEvidenceFileStore>();
            services.AddScoped<IAccountingContextSource, CsvAccountingContextSource>();

            return services;
        }
    }
}
