namespace SignalRChat.Api.Contracts;

public record CreateUserRequest(
    string Username,
    string Email
);