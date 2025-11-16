using StackExchange.Redis;

namespace Shared;

public class RedisIdempotencyStore
{
    private readonly IDatabase _db;
    private readonly TimeSpan _ttl = TimeSpan.FromHours(1);

    public RedisIdempotencyStore(string redisConn = "localhost:6379")
    {
        var conn = ConnectionMultiplexer.Connect(redisConn);
        _db = conn.GetDatabase();
    }

    public bool IsProcessed(string messageId) => _db.KeyExists(messageId);

    public void MarkProcessed(string messageId)
    {
        _db.StringSet(messageId, "1", _ttl);
    }
}