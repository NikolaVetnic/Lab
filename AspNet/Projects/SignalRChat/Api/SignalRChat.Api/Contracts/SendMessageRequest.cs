namespace SignalRChat.Api.Contracts;

public record SendMessageRequest(
    Guid SenderId,
    string Text
);