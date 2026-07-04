namespace BuildingBlocks.Cqrs.Abstractions;

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public interface IMediator : ISender;
