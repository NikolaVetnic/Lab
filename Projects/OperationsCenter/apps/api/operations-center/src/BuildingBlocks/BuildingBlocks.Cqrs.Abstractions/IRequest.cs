namespace BuildingBlocks.Cqrs.Abstractions;

public interface IRequest<out TResponse>;

public readonly record struct Unit;
