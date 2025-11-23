using System.Collections.Concurrent;

namespace Shared
{
    public class IdempotencyStore
    {
        private readonly ConcurrentDictionary<string, DateTime> _processed = new();
        private readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        public bool IsProcessed(string messageId) => _processed.ContainsKey(messageId);

        public void MarkProcessed(string messageId)
        {
            _processed[messageId] = DateTime.UtcNow;
            Cleanup();
        }

        private void Cleanup()
        {
            var cutoff = DateTime.UtcNow - _ttl;
            var keys = _processed.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
            foreach (var k in keys) _processed.TryRemove(k, out _);
        }
    }
}