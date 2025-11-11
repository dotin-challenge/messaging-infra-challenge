using ErrorWorker.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;



var factory = new ConnectionFactory
{
	HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
	UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
	Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest",
	DispatchConsumersAsync = true
};
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.BasicQos(0, 1, false);
channel.QueueDeclare("logs.error.q", durable: true, exclusive: false, autoDelete: false);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	var body = ea.Body.ToArray();
	var log = JsonSerializer.Deserialize<LogMessage>(Encoding.UTF8.GetString(body));

	Console.WriteLine($"[Worker workerId] Received Error: {log?.Message}");

	try
	{
		await Task.Delay(1000); // simulate work
		channel.BasicAck(ea.DeliveryTag, false);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[Worker workerId] Error: {ex.Message}");
		await Task.Delay(2000); // backoff
		channel.BasicNack(ea.DeliveryTag, false, true);
	}
};

channel.BasicConsume("logs.error.q", autoAck: false, consumer);
Console.WriteLine($"[Worker workerId] Waiting for Error logs...");
await Task.Delay(Timeout.Infinite);
