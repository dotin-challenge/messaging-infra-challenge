using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace InfoSubscriber;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var serviceName = args.Length > 0 ? args[0] : $"info-{Environment.MachineName}";
        var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
        var prefetch = int.TryParse(Environment.GetEnvironmentVariable(SharedConstants.PrefetchEnv), out var p) ? p : 10;

        using var rabbit = new RabbitConnection(uri, prefetch);
        var channel = rabbit.Channel;

        var queueName = $"logs.info.q.{serviceName}";
        channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queueName, SharedConstants.InfoExchange, "");

        void RegisterConsumer()
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (ch, ea) =>
            {
                var body = ea.Body.ToArray();
                try
                {
                    var msg = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(body));
                    if (msg != null)
                        Console.WriteLine($"[InfoSub-{serviceName}] {msg.Id} -> dashboard update");
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch { channel.BasicNack(ea.DeliveryTag, false, false); }
            };
            channel.BasicConsume(queueName, autoAck: false, consumer: consumer);
        }

        RegisterConsumer();

        var done = new TaskCompletionSource<bool>();
        AssemblyLoadContext.Default.Unloading += ctx => done.TrySetResult(true);
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; done.TrySetResult(true); };

        Console.WriteLine($"[InfoSub-{serviceName}] Listening...");
        await done.Task;

        return 0;
    }
}