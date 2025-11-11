using InfoSubscriber.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;


var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "app1";

var factory = new ConnectionFactory
{
	HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
	UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
	Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest",
	DispatchConsumersAsync = true
};
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();



channel.ExchangeDeclare("logs.info.exchange", ExchangeType.Fanout, durable: true);
var queueName = $"logs.info.q.{serviceName}";
channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
channel.QueueBind(queueName, "logs.info.exchange", "");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	var body = ea.Body.ToArray();
	var log = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(body));
	Console.WriteLine($"[{serviceName}] Info Received: {log?.Message}");
	await Task.CompletedTask;
};

channel.BasicConsume(queueName, autoAck: true, consumer);
Console.WriteLine($"[{serviceName}] Subscribed to Info logs...");
await Task.Delay(Timeout.Infinite);