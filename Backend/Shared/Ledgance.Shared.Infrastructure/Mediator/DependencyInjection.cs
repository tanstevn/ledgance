using Ledgance.Shared.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Ledgance.Shared.Infrastructure.Mediator {
    public static class DependencyInjection {
        public static void AddMediatorFromAssemblies(this IServiceCollection services, params Assembly[] assemblies) {
            services.AddScoped<IMediator, Mediator>();

            foreach (var assembly in assemblies) {
                var assemblyTypes = assembly.GetTypes();

                services.AddRequestHandlers(assemblyTypes);
                services.AddPipelineBehaviors(assemblyTypes);
            }
        }

        private static void AddRequestHandlers(this IServiceCollection services, Type[] types) {
            var handlerTypes = types
                .Where(type => type.IsClass && !type.IsAbstract)
                .SelectMany(type => type.GetInterfaces()
                    .Where(@interface => @interface.IsGenericType
                        && @interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)),
                    (type, @interface) => new {
                        Interface = @interface,
                        Implementation = type
                    });

            foreach (var type in handlerTypes) {
                // Registering the Request Handler(s)
                // as Open-Generic (e.g., typeof(IRequestHandler<,>))
                services.AddTransient(type.Interface, type.Implementation);
            }
        }

        private static void AddPipelineBehaviors(this IServiceCollection services, Type[] types) {
            var behaviorTypes = types
                .Where(type => type.IsClass && !type.IsAbstract
                    && type.IsGenericTypeDefinition
                    && type.GetInterfaces()
                        .Any(intf => intf.IsGenericType
                            && intf.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)));

            foreach (var type in behaviorTypes) {
                // Same as how request handlers are registered
                services.AddTransient(typeof(IPipelineBehavior<,>), type);
            }
        }
    }
}
