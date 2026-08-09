namespace Ledgance.Shared.Application.Abstractions {
    public interface ICommand<out TResponse> : IRequest<TResponse> { }
}
