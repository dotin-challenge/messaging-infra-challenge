using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

const string INFO_EXCHANGE = "logs.info.exchange";

var serviceName = args.Length > 0 ? args[0] : "default";
var queueName = $"logs.info.q.{serviceName}";

var amqpUri = Environment.GetEnvironmentVariable("AMQP_URI")
    ?? $"amqp://{Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest"}:{Environment.GetEnvironmentVariable("RABBIT_PASS") ?? "guest"}@{Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "localhost"}:5672/";

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

IConnection? connection = null;
IChannel? channel = null;

try
{
    var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };

    connection = await factory.CreateConnectionAsync();
    channel = await connection.CreateChannelAsync();

    await channel.ExchangeDeclareAsync(INFO_EXCHANGE, ExchangeType.Fanout, durable: true);
    await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
    await channel.QueueBindAsync(queueName, INFO_EXCHANGE, routingKey: "");

    Console.WriteLine($"[InfoSub-{serviceName}] Subscribed to info logs. Press Ctrl+C to exit.\n");

    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            var log = JsonSerializer.Deserialize<JsonElement>(message);
            var id = log.GetProperty("id").GetString();
            var msg = log.GetProperty("msg").GetString();
            var latency = log.GetProperty("latency_ms").GetInt32();

            Console.WriteLine($"[InfoSub-{serviceName}] {id} -> {msg} (latency: {latency}ms)");

            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InfoSub-{serviceName}] Error processing message: {ex.Message}");
            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
        }
    };

    await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(100, cts.Token);
    }
}
catch (OperationCanceledException)
{
}
catch (Exception ex)
{
    Console.WriteLine($"[InfoSub-{serviceName}] Fatal error: {ex.Message}");
    return 1;
}
finally
{
    if (channel != null)
    {
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }

    if (connection != null)
    {
        await connection.CloseAsync();
        await connection.DisposeAsync();
    }

    Console.WriteLine($"\n[InfoSub-{serviceName}] Shutdown complete.");
}

return 0;
