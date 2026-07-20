namespace hhSalon.Services.Services.Implementations
{
	public class ChatService
	{
		private readonly Dictionary<string, HashSet<string>> _connections = new();
		private readonly object _sync = new();

		public void AddUserConnectionId(string userId, string connectionId)
		{
			if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
				return;

			lock (_sync)
			{
				if (!_connections.TryGetValue(userId, out var connections))
				{
					connections = new HashSet<string>();
					_connections[userId] = connections;
				}
				connections.Add(connectionId);
			}
		}

		public IReadOnlyCollection<string> GetConnectionIdsByUserId(string userId)
		{
			lock (_sync)
			{
				return _connections.TryGetValue(userId, out var connections)
					? connections.ToList()
					: Array.Empty<string>();
			}
		}

		public void RemoveConnection(string connectionId)
		{
			lock (_sync)
			{
				foreach (var entry in _connections.ToList())
				{
					entry.Value.Remove(connectionId);
					if (entry.Value.Count == 0)
						_connections.Remove(entry.Key);
				}
			}
		}
	}
}
