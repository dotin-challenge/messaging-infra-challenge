using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

const string ERROR_EXCHANGE = "logs.error.exchange";
const string ERROR_QUEUE = "logs.error.q";
const string ERROR_ROUTING_KEY = "error";
const string INFO_EXCHANGE = "logs.info.exchange";

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

    await channel.ExchangeDeclareAsync(ERROR_EXCHANGE, ExchangeType.Direct, durable: true);
    await channel.QueueDeclareAsync(ERROR_QUEUE, durable: true, exclusive: false, autoDelete: false);
    await channel.QueueBindAsync(ERROR_QUEUE, ERROR_EXCHANGE, ERROR_ROUTING_KEY);

    await channel.ExchangeDeclareAsync(INFO_EXCHANGE, ExchangeType.Fanout, durable: true);

    var properties = new BasicProperties { Persistent = true };

    var random = new Random();
    var services = new[] { "auth", "web", "payment", "inventory" };
    var errorMessages = new[] { "DB timeout", "Connection refused", "Memory overflow", "Null reference" };
    var severities = new[] { "HIGH", "MEDIUM", "CRITICAL" };

    Console.WriteLine("Producer started. Press Ctrl+C to exit.\n");

    var counter = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        counter++;

        try
        {
            if (counter % 3 == 0)
            {
                var errorId = $"E-{random.Next(1000, 9999)}";
                var service = services[random.Next(services.Length)];
                var message = errorMessages[random.Next(errorMessages.Length)];
                var severity = severities[random.Next(severities.Length)];

                var errorLog = new
                {
                    id = errorId,
                    service,
                    msg = message,
                    severity,
                    timestamp = DateTime.UtcNow
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorLog));

                await channel.BasicPublishAsync(
                    exchange: ERROR_EXCHANGE,
                    routingKey: ERROR_ROUTING_KEY,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                Console.WriteLine($"[Producer] Sent Error id={errorId} service={service} msg=\"{message}\" severity={severity}");
            }
            else
            {
                var infoId = $"I-{random.Next(1000, 9999)}";
                var service = services[random.Next(services.Length)];
                var latency = random.Next(10, 200);
                var apiMessage = $"GET /api/{service} 200";

                var infoLog = new
                {
                    id = infoId,
                    service,
                    msg = apiMessage,
                    latency_ms = latency,
                    timestamp = DateTime.UtcNow
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(infoLog));

                await channel.BasicPublishAsync(
                    exchange: INFO_EXCHANGE,
                    routingKey: "",
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                Console.WriteLine($"[Producer] Sent Info  id={infoId} service={service} msg=\"{apiMessage}\" latency_ms={latency}");
            }

            await Task.Delay(1000, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Producer] Error publishing message: {ex.Message}");
            await Task.Delay(2000, cts.Token);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Producer] Fatal error: {ex.Message}");
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

    Console.WriteLine("\n[Producer] Shutdown complete.");
}

return 0;
