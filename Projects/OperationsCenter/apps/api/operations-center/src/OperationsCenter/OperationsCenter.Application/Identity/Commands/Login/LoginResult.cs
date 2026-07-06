using OperationsCenter.Application.Identity.Contracts;

namespace OperationsCenter.Application.Identity.Commands.Login;

public enum LoginOutcome
{
    Success = 0,
    InvalidCredentials = 1,
    UserInactive = 2
}

public sealed record LoginResult(LoginOutcome Outcome, LoginResponse? Response)
{
    public static LoginResult Succeeded(LoginResponse response) => new(LoginOutcome.Success, response);

    public static LoginResult Failed(LoginOutcome outcome) => new(outcome, null);
}
