using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "ErrorWorkerService";

var factory = new ConnectionFactory
{
    HostName = host,
    UserName = user,
    Password = pass,
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
    RequestedHeartbeat = TimeSpan.FromSeconds(30)
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
channel.ExchangeDeclare("logs.error.exchange", ExchangeType.Direct, durable: true);
channel.QueueDeclare("error.queue", durable: true, exclusive: false, autoDelete: false);
channel.QueueBind("error.queue", "logs.error.exchange", "error");

Console.WriteLine($"[{serviceName}] Waiting for error logs...");

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[{serviceName}] ERROR received: {message}");
    Console.ResetColor();

    try
    {
        ProcessError(message);

        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{serviceName}] Failed to process error: {ex.Message}");
        channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
    }
};

channel.BasicConsume(queue: "error.queue", autoAck: false, consumer: consumer);

Console.WriteLine($"[{serviceName}] Listening... Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);

static void ProcessError(string message)
{
    Console.WriteLine($"[ErrorWorker] Processed error: {message}");
}
