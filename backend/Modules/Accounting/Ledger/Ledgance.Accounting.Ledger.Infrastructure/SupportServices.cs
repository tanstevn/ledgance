using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Application.Published;
using Microsoft.Extensions.DependencyInjection;
using SupabaseClient = Supabase.Client;

namespace Ledgance.Accounting.Ledger.Infrastructure {
    internal sealed class SupabaseDocumentFileStore : IDocumentFileStore {
        public const string BucketName = "accounting-documents";

        private readonly SupabaseClient _client;

        public SupabaseDocumentFileStore(SupabaseClient client) {
            _client = client;
        }

        public async Task<string> UploadAsync(Guid entityId, Guid documentId, string fileName,
            byte[] content, string contentType, CancellationToken ct) {
            var path = $"{entityId}/{documentId}/{fileName}";

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

        public async Task<string> CreateDownloadUrlAsync(string storagePath, TimeSpan lifetime,
            CancellationToken ct) =>
            await _client.Storage
                .From(BucketName)
                .CreateSignedUrl(storagePath, (int)lifetime.TotalSeconds);
    }

    public static class LedgerInfrastructureExtensions {
        public static IServiceCollection AddAccountingLedgerInfrastructure(
            this IServiceCollection services) {
            services.AddScoped<IEntityRepository, EntityRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IFiscalPeriodRepository, FiscalPeriodRepository>();
            services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
            services.AddScoped<ILedgerLineRepository, LedgerLineRepository>();
            services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IDocumentFileStore, SupabaseDocumentFileStore>();
            services.AddScoped<IEntityGuard, EntityGuard>();
            services.AddScoped<IAccountingReadContract, AccountingReadContract>();

            return services;
        }
    }
}
