using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Cqrs.Abstractions;

namespace BuildingBlocks.Cqrs;

public sealed class InProcessMediator(IServiceProvider serviceProvider) : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handler = ResolveHandler(request, requestType, responseType, handlerType)
            ?? throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");

        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle))
            ?? throw new InvalidOperationException($"Handler for '{requestType.FullName}' does not expose Handle method.");

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviors = serviceProvider.GetServices(behaviorType)
            .Where(static behavior => behavior is not null)
            .ToArray();

        RequestHandlerDelegate<TResponse> handlerDelegate = () => Invoke<TResponse>(
            handleMethod,
            handler,
            request,
            cancellationToken,
            requestType,
            "handler");

        foreach (var behavior in behaviors.Reverse())
        {
            var next = handlerDelegate;
            handlerDelegate = () =>
            {
                var behaviorMethod = behaviorType.GetMethod(nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.Handle))
                    ?? throw new InvalidOperationException($"Behavior for '{requestType.FullName}' does not expose Handle method.");

                return Invoke<TResponse>(
                    behaviorMethod,
                    behavior!,
                    request,
                    cancellationToken,
                    requestType,
                    "behavior",
                    next);
            };
        }

        return handlerDelegate();
    }

    private object? ResolveHandler<TResponse>(
        IRequest<TResponse> request,
        Type requestType,
        Type responseType,
        Type requestHandlerType)
    {
        var handler = serviceProvider.GetService(requestHandlerType);
        if (handler is not null)
        {
            return handler;
        }

        if (request is ICommand<TResponse>)
        {
            var commandHandlerType = typeof(ICommandHandler<,>).MakeGenericType(requestType, responseType);
            handler = serviceProvider.GetService(commandHandlerType);
            if (handler is not null)
            {
                return handler;
            }
        }

        var queryInterfaceType = typeof(IQuery<>).MakeGenericType(responseType);
        if (queryInterfaceType.IsAssignableFrom(requestType))
        {
            var queryHandlerType = typeof(IQueryHandler<,>).MakeGenericType(requestType, responseType);
            handler = serviceProvider.GetService(queryHandlerType);
        }

        return handler;
    }

    private static Task<TResponse> Invoke<TResponse>(
        System.Reflection.MethodInfo method,
        object target,
        IRequest<TResponse> request,
        CancellationToken cancellationToken,
        Type requestType,
        string componentType,
        RequestHandlerDelegate<TResponse>? next = null)
    {
        var parameters = next is null
            ? new object?[] { request, cancellationToken }
            : new object?[] { request, next, cancellationToken };

        var result = method.Invoke(target, parameters);
        if (result is Task<TResponse> typedTask)
        {
            return typedTask;
        }

        throw new InvalidOperationException(
            $"Configured {componentType} for '{requestType.FullName}' returned an unexpected response type.");
    }
}
