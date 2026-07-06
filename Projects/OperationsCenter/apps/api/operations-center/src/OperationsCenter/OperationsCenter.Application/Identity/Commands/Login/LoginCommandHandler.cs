using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Application.Identity.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Identity.Commands.Login;

public sealed class LoginCommandHandler(
    IOperationsCenterDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator)
    : ICommandHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return LoginResult.Failed(LoginOutcome.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return LoginResult.Failed(LoginOutcome.UserInactive);
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return LoginResult.Failed(LoginOutcome.InvalidCredentials);
        }

        var tokenResult = accessTokenGenerator.Generate(user);

        return LoginResult.Succeeded(new LoginResponse(
            AccessToken: tokenResult.AccessToken,
            ExpiresAtUtc: tokenResult.ExpiresAtUtc));
    }
}
