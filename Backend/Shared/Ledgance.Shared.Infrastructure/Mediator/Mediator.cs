using Ledgance.Shared.Application.Abstractions;
using System.Collections.Concurrent;

namespace Ledgance.Shared.Infrastructure.Mediator {
    internal class Mediator : IMediator {
        private static readonly ConcurrentDictionary<(Type Request, Type Response), IExecutor> Executors = new();

        private readonly IServiceProvider _provider;

        public Mediator(IServiceProvider provider) {
            _provider = provider;
        }

        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct) {
            ArgumentNullException.ThrowIfNull(request);

            var executor = Executors.GetOrAdd((request.GetType(), typeof(TResponse)),
                static key => (IExecutor)Activator.CreateInstance(
                    typeof(Executor<,>).MakeGenericType(key.Request, key.Response))!);

            var result = await executor.ExecuteAsync(request, _provider, ct);
            return (TResponse)result;
        }
    }
}
