namespace SignalRChat.Api.Contracts;

public record ChatMessageResponse(
    Guid Id,
    Guid SenderId,
    string SenderUsername,
    string Text,
    DateTime SentAtUtc
);