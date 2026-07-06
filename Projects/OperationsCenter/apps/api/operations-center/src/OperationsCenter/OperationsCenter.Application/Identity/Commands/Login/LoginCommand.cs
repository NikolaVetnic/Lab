using BuildingBlocks.Cqrs.Abstractions;

namespace OperationsCenter.Application.Identity.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
