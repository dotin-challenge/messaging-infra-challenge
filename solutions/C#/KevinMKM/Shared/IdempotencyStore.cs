using System.Collections.Concurrent;

namespace Shared
{
    public class IdempotencyStore
    {
        private readonly ConcurrentDictionary<string, DateTime> _processed = new();
        private readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        public bool IsProcessed(string messageId)
        {
            return _processed.ContainsKey(messageId);
        }

        public void MarkProcessed(string messageId)
        {
            _processed[messageId] = DateTime.UtcNow;
            Cleanup();
        }

        private void Cleanup()
        {
            var keysToRemove = _processed.Where(kv => DateTime.UtcNow - kv.Value > _ttl)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in keysToRemove)
                _processed.TryRemove(key, out _);
        }
    }
}