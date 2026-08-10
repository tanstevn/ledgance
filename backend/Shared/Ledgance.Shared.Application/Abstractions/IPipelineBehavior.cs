namespace Ledgance.Shared.Application.Abstractions {
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : notnull {
        Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
    }

    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
}
