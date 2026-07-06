namespace OperationsCenter.Application.Identity.Contracts;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string TokenType = "Bearer");
