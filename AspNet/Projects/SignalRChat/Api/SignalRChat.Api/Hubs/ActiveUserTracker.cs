using System.Collections.Concurrent;

namespace SignalRChat.Api.Hubs;

public sealed class ActiveUserTracker
{
    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<string, byte>
    > _userConnections = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        var connections = _userConnections.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, byte>()
        );

        connections.TryAdd(connectionId, 0);
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (!_userConnections.TryGetValue(userId, out var connections))
        {
            return;
        }

        connections.TryRemove(connectionId, out _);

        if (connections.IsEmpty)
        {
            _userConnections.TryRemove(userId, out _);
        }
    }

    public IReadOnlyList<Guid> GetActiveUserIds()
    {
        return _userConnections.Keys.ToList();
    }

    public int GetConnectionCount(Guid userId)
    {
        return _userConnections.TryGetValue(userId, out var connections)
            ? connections.Count
            : 0;
    }

    public int GetActiveUserCount()
    {
        return _userConnections.Count;
    }
}