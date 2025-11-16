using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

const string ERROR_QUEUE = "logs.error.q";

var workerId = Environment.GetEnvironmentVariable("WORKER_ID") ?? Guid.NewGuid().ToString()[..8];

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

    await channel.QueueDeclareAsync(ERROR_QUEUE, durable: true, exclusive: false, autoDelete: false);

    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

    Console.WriteLine($"[ErrorWorker-{workerId}] Waiting for error messages. Press Ctrl+C to exit.\n");

    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            var log = JsonSerializer.Deserialize<JsonElement>(message);
            var id = log.GetProperty("id").GetString();
            var severity = log.GetProperty("severity").GetString();
            var msg = log.GetProperty("msg").GetString();

            Console.WriteLine($"[ErrorWorker-{workerId}] {id} received (severity={severity})");

            var processingTime = severity switch
            {
                "CRITICAL" => 3000,
                "HIGH" => 2000,
                "MEDIUM" => 1000,
                _ => 500
            };

            await Task.Delay(processingTime);

            Console.WriteLine($"[ErrorWorker-{workerId}] {id} processed successfully → acked");

            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ErrorWorker-{workerId}] Error processing message: {ex.Message}");
            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
        }
    };

    await channel.BasicConsumeAsync(queue: ERROR_QUEUE, autoAck: false, consumer: consumer);

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
    Console.WriteLine($"[ErrorWorker-{workerId}] Fatal error: {ex.Message}");
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

    Console.WriteLine($"\n[ErrorWorker-{workerId}] Shutdown complete.");
}

return 0;
