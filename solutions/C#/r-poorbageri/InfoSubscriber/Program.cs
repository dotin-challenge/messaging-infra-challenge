using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "InfoSubscriberService";

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

var dlxName = "logs.info.dlx";
var dlqName = $"logs.info.dlq.{serviceName}";
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
channel.ExchangeDeclare(dlxName, ExchangeType.Fanout, durable: true);
channel.QueueDeclare(queue: dlqName, durable: true, exclusive: false, autoDelete: false);
channel.QueueBind(queue: dlqName, exchange: dlxName, routingKey: "");

var queueArgs = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", dlxName }
};
var queueName = $"logs.info.q.{serviceName}";
channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);


Console.WriteLine($"[{serviceName}] Waiting for info logs...");

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[{serviceName}] INFO received: {message}");
    Console.ResetColor();

    try
    {
        ProcessInfo(message);
        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Error] service={serviceName} type=InfoSubscriber error=\"{ex.Message}\" stack=\"{ex.StackTrace}\"");
        channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
    }
};

channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

Console.WriteLine($"[{serviceName}] Listening... Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);

static void ProcessInfo(string message)
{
    Console.WriteLine($"[InfoSubscriber] Processed info: {message}");
}
