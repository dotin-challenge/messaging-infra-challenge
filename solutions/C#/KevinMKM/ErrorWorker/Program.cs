using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace ErrorWorker;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
        var prefetch = int.TryParse(Environment.GetEnvironmentVariable(SharedConstants.PrefetchEnv), out var p) ? p : 1;
        var redisConn = Environment.GetEnvironmentVariable("REDIS_URI") ?? "localhost:6379";

        using var rabbit = new RabbitConnection(uri, prefetch);
        var channel = rabbit.Channel;
        var idempotency = new RedisIdempotencyStore(redisConn);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            LogMessage? msg = null;
            try { msg = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(body)); }
            catch { channel.BasicNack(ea.DeliveryTag, false, false); return; }

            if (msg == null || idempotency.IsProcessed(msg.Id))
            {
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            Console.WriteLine($"[ErrorWorker] Received {msg.Id} - processing...");
            try
            {
                await Task.Delay(new Random().Next(200, 1200));
                idempotency.MarkProcessed(msg.Id);
                channel.BasicAck(ea.DeliveryTag, false);
                Console.WriteLine($"[ErrorWorker] Processed {msg.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ErrorWorker] Error {msg.Id}: {ex.Message}, sending to DLX");
                channel.BasicNack(ea.DeliveryTag, false, false);
            }
        };

        var consumerTag = channel.BasicConsume(SharedConstants.ErrorQueue, autoAck: false, consumer);

        var done = new TaskCompletionSource<bool>();
        AssemblyLoadContext.Default.Unloading += ctx => done.TrySetResult(true);
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; done.TrySetResult(true); };

        Console.WriteLine("[ErrorWorker] Listening for error messages...");
        await done.Task;
        try { channel.BasicCancel(consumerTag); } catch { }

        return 0;
    }
}