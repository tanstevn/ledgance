namespace Ledgance.Shared.Application.Abstractions {
    public interface IExecutor {
        Task<object> ExecuteAsync(object request, IServiceProvider provider, CancellationToken ct);
    }
}
