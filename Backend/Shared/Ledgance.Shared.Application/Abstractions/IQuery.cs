namespace Ledgance.Shared.Application.Abstractions {
    public interface IQuery<out TResponse> : IRequest<TResponse> { }
}
