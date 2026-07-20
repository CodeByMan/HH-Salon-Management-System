using System.Collections.Concurrent;

namespace hhSalonAPI.Hubs
{
	public class ChatMessageRateLimiter
	{
		private readonly ConcurrentDictionary<string, Queue<DateTime>> _messages = new();
		private readonly object _sync = new();

		public bool TryAcquire(string userId, int permitLimit = 30, int windowSeconds = 60)
		{
			lock (_sync)
			{
				var now = DateTime.UtcNow;
				var queue = _messages.GetOrAdd(userId, _ => new Queue<DateTime>());
				while (queue.Count > 0 && now - queue.Peek() >= TimeSpan.FromSeconds(windowSeconds))
					queue.Dequeue();
				if (queue.Count >= permitLimit)
					return false;
				queue.Enqueue(now);
				return true;
			}
		}
	}
}
