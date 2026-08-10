namespace Ledgance.Shared.Application.Abstractions {
    public interface IMediator {
        Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct);
    }
}
