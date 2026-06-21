using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRChat.Api.Contracts;
using SignalRChat.Api.Data;
using SignalRChat.Api.Entities;

namespace SignalRChat.Api.Hubs;

public class ChatHub(ChatDbContext dbContext, ActiveUserTracker activeUserTracker) : Hub
{
    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            Context.Abort();
            return;
        }

        var wasAlreadyOnline = activeUserTracker.GetConnectionCount(userId) > 0;

        activeUserTracker.AddConnection(
            userId,
            Context.ConnectionId
        );

        Console.WriteLine(
            $"User {userId} connected. " +
            $"Active connections: {activeUserTracker.GetConnectionCount(userId)}"
        );

        if (!wasAlreadyOnline)
        {
            var user = await dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => new ChatUserResponse(
                    u.Id,
                    u.Username,
                    u.Email
                ))
                .SingleOrDefaultAsync();

            if (user is not null)
            {
                await Clients.All.SendAsync("UserConnected", user);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            activeUserTracker.RemoveConnection(userId, Context.ConnectionId);

            var remainingConnections =
                activeUserTracker.GetConnectionCount(userId);

            Console.WriteLine(
                $"User {userId} disconnected. " +
                $"Remaining connections: {remainingConnections}"
            );

            if (remainingConnections == 0)
            {
                await Clients.All.SendAsync("UserDisconnected", userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    public async Task SendMessage(SendMessageRequest request)
    {
        var sender = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.SenderId) ??
            throw new HubException("Sender not found.");

        var message = new ChatMessage
        {
            SenderId = sender.Id,
            Sender = sender,
            Text = request.Text,
            SentAtUtc = DateTime.UtcNow
        };

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();

        var response = new ChatMessageResponse(
            message.Id,
            sender.Id,
            sender.Username,
            message.Text,
            message.SentAtUtc
        );

        await Clients.All.SendAsync("ReceiveMessage", response);
    }
}