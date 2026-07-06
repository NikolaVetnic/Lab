using OperationsCenter.Domain.Identity;

namespace OperationsCenter.Application.Identity.Abstractions;

public interface IAccessTokenGenerator
{
    AccessTokenResult Generate(User user);
}

public sealed record AccessTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
