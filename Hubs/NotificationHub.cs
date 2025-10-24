using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BookInfoFinder.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        // In-memory map of userId -> connection ids for debugging/inspection
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _userConnections =
            new();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        // Client calls Register to join a group for the user's id
        public async Task Register(int userId)
        {
            var group = GetUserGroup(userId);
            await Groups.AddToGroupAsync(Context.ConnectionId, group);

            var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            connections[Context.ConnectionId] = 0;

            _logger.LogInformation("SignalR: connection {ConnectionId} registered for user {UserId} (group={Group})",
                Context.ConnectionId, userId, group);
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("SignalR: connection {ConnectionId} connected", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Remove connection id from any user mapping entries
            try
            {
                foreach (var kvp in _userConnections)
                {
                    var userId = kvp.Key;
                    var dict = kvp.Value;
                    if (dict.TryRemove(Context.ConnectionId, out _))
                    {
                        _logger.LogInformation("SignalR: connection {ConnectionId} removed from user {UserId} on disconnect",
                            Context.ConnectionId, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR: error while cleaning up connection {ConnectionId}", Context.ConnectionId);
            }

            _logger.LogInformation("SignalR: connection {ConnectionId} disconnected", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        // Helper to construct group name
        public static string GetUserGroup(int userId) => $"user-{userId}";

        // Debug helper to inspect connections for a given user
        public static IEnumerable<string> GetConnectionsForUser(int userId)
        {
            if (_userConnections.TryGetValue(userId, out var dict))
            {
                return dict.Keys;
            }

            return Enumerable.Empty<string>();
        }
    }
}
