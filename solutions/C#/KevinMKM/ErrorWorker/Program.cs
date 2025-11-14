using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared;

namespace ErrorWorker;

public class Program
{
    public static void Main(string[] args)
    {
        var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
        using var rabbit = new RabbitConnection(uri);
        var channel = rabbit.Channel;
        var idempotency = new IdempotencyStore();
        var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            var msg = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(ea.Body.ToArray()));
            if (msg == null) 
                return;

            if (idempotency.IsProcessed(msg.Id))
            {
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [ErrorWorker-{Environment.MachineName}] Processing {msg.Id}");
                await Task.Delay(new Random().Next(100, 1000)); // Simulate processing
                channel.BasicAck(ea.DeliveryTag, false);
                idempotency.MarkProcessed(msg.Id);
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [ErrorWorker-{Environment.MachineName}] Completed {msg.Id}");
            }
            catch
            {
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(SharedConstants.ErrorQueue, autoAck: false, consumer);
        Console.WriteLine("[ErrorWorker] Listening...");
        Console.ReadLine();
    }
}