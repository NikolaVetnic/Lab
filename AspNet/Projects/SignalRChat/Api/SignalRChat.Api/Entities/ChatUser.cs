namespace SignalRChat.Api.Entities;

public class ChatUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ICollection<ChatMessage> SentMessages { get; set; } = [];
}