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
        var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ??
                  "amqp://guest:guest@localhost:5672/";
        var prefetch = int.TryParse(Environment.GetEnvironmentVariable(SharedConstants.PrefetchEnv), out var p) ? p : 1;

        using var rabbit = new RabbitConnection(uri, prefetch);
        var channel = rabbit.Channel;
        channel.BasicQos(0, (ushort)prefetch, false);

        var idempotency = new IdempotencyStore();

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            LogMessage? msg = null;
            try
            {
                msg = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(body));
            }
            catch
            {
                Console.WriteLine("[ErrorWorker] Malformed message - acking to drop");
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            if (msg == null)
            {
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            if (idempotency.IsProcessed(msg.Id))
            {
                Console.WriteLine($"[ErrorWorker] Duplicate {msg.Id} -> ack");
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
                Console.WriteLine($"[ErrorWorker] Error processing {msg.Id}: {ex.Message}. Sending to DLX");
                channel.BasicNack(ea.DeliveryTag, false, requeue: false);
            }
        };

        var consumerTag = channel.BasicConsume(SharedConstants.ErrorQueue, autoAck: false, consumer);

        var done = new TaskCompletionSource<bool>();
        AssemblyLoadContext.Default.Unloading += ctx => { done.TrySetResult(true); };
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            done.TrySetResult(true);
        };

        Console.WriteLine("[ErrorWorker] Listening for error messages...");
        await done.Task;
        try
        {
            channel.BasicCancel(consumerTag);
        }
        catch
        {
        }

        return 0;
    }
}