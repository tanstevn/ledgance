using Ledgance.Shared.Application.Abstractions;

namespace Ledgance.Shared.Infrastructure.Mediator {
    internal class Mediator : IMediator {
        private readonly IServiceProvider _provider;

        public Mediator(IServiceProvider provider) {
            _provider = provider;
        }

        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct) {
            ArgumentNullException.ThrowIfNull(request);

            var executorType = typeof(Executor<,>)
                .MakeGenericType(request.GetType(), typeof(TResponse));

            var requestHandler = (IExecutor)Activator.CreateInstance(executorType)!;

            var result = await requestHandler.ExecuteAsync(request, _provider, ct);
            return (TResponse)result;
        }
    }
}
