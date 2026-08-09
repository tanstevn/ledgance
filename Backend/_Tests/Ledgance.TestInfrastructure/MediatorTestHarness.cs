using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure;
using Ledgance.Shared.Infrastructure.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ledgance.TestInfrastructure {
    /// <summary>
    /// Dispatches requests through the real mediator and the real cross-cutting behaviors, so a
    /// slice test exercises authorization, entitlements and validation the way production does.
    /// </summary>
    public sealed class MediatorTestHarness {
        private readonly ServiceCollection _services = [];
        private ServiceProvider? _provider;

        public MediatorTestHarness(CurrentUser? user = null) {
            CurrentUser = new FakeCurrentUserAccessor(user);
            Entitlements = new FakeEntitlementService();

            _services.AddSingleton<ICurrentUserAccessor>(CurrentUser);
            _services.AddSingleton<ICurrentUserInitializer>(CurrentUser);
            _services.AddSingleton<IEntitlementService>(Entitlements);
            _services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
                typeof(NullLogger<>));

            _services.AddMediatorFromAssemblies(
                typeof(SharedInfrastructureExtensions).Assembly);
        }

        public FakeCurrentUserAccessor CurrentUser { get; }

        public FakeEntitlementService Entitlements { get; }

        public MediatorTestHarness WithHandler<TRequest, TResponse, THandler>()
            where TRequest : IRequest<TResponse>
            where THandler : class, IRequestHandler<TRequest, TResponse> {
            _services.AddTransient<IRequestHandler<TRequest, TResponse>, THandler>();
            return this;
        }

        public MediatorTestHarness WithHandler<TRequest, TResponse>(
            IRequestHandler<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse> {
            _services.AddSingleton(handler);
            return this;
        }

        public MediatorTestHarness WithValidator<TRequest>(IValidator<TRequest> validator) {
            _services.AddSingleton(validator);
            return this;
        }

        public MediatorTestHarness WithService<TService>(TService instance)
            where TService : class {
            _services.AddSingleton(instance);
            return this;
        }

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request,
            CancellationToken ct = default) {
            _provider ??= _services.BuildServiceProvider();

            return _provider.GetRequiredService<IMediator>().SendAsync(request, ct);
        }
    }
}
