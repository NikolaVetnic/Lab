namespace SignalRChat.Api.Contracts;

public record ChatUserResponse(
    Guid Id,
    string Username,
    string Email
);