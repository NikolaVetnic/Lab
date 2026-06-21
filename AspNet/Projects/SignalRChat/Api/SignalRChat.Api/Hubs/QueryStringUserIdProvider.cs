using Microsoft.AspNetCore.SignalR;

namespace SignalRChat.Api.Hubs;

public sealed class QueryStringUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.GetHttpContext()?
            .Request
            .Query["userId"]
            .FirstOrDefault();
    }
}