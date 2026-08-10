using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Ledgance.Shared.Infrastructure.Mediator {
    internal class Executor<TRequest, TResponse> : IExecutor
       where TRequest : IRequest<TResponse> {
        public async Task<object> ExecuteAsync(object request, IServiceProvider provider, CancellationToken ct) {
            var requestHandler = provider
                .GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> finalHandler =
                () => requestHandler.HandleAsync((TRequest)request, ct);

            var behaviors = provider
                .GetServices<IPipelineBehavior<TRequest, TResponse>>();

            var behaviorsOrder = behaviors.OrderByDescending(
                behavior => behavior.GetType()
                    .GetCustomAttribute<PipelineOrderAttribute>()
                    ?.Order ?? short.MaxValue);

            var aggregateResult = behaviorsOrder.Aggregate(finalHandler, (next, behavior) =>
                () => behavior.HandleAsync((TRequest)request, next, ct));

            return (await aggregateResult())!;
        }
    }
}
