using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared;

namespace InfoSubscriber;

public class Program
{
    public static void Main(string[] args)
    {
        var service = args.Length > 0 ? args[0] : "unknown";
        var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
        using var rabbit = new RabbitConnection(uri);
        var channel = rabbit.Channel;
        var queueName = $"logs.info.q.{service}";
        channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queueName, SharedConstants.InfoExchange, "");
        var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer(channel);
        consumer.Received += async (ch, ea) =>
        {
            var msg = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(ea.Body.ToArray()));
            if (msg == null)
                return;

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [InfoSub-{service}] {msg.Id} -> dashboard updated");
            channel.BasicAck(ea.DeliveryTag, false);
        };

        channel.BasicConsume(queueName, autoAck: false, consumer);
        Console.WriteLine($"[InfoSub-{service}] Listening...");
        Console.ReadLine();
    }
}