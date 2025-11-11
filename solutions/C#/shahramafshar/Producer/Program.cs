using Producer.Models;
using RabbitMQ.Client;
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

channel.ConfirmSelect();

// Error Exchange
channel.ExchangeDeclare("logs.error.exchange", ExchangeType.Direct, durable: true);
channel.QueueDeclare("logs.error.q", durable: true, exclusive: false, autoDelete: false);
channel.QueueBind("logs.error.q", "logs.error.exchange", routingKey: "error");

// Info Exchange
channel.ExchangeDeclare("logs.info.exchange", ExchangeType.Fanout, durable: true);

var rnd = new Random();
for (int i = 0; i < 10; i++)
{
	var log = new LogMessage
	{
		Timestamp = DateTime.UtcNow,
		Level = i % 2 == 0 ? "Error" : "Info",
		Message = $"Log message {i}",
		Service = "ProducerService"
	};

	var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(log));
	var props = channel.CreateBasicProperties();
	props.Persistent = true;

	if (log.Level == "Error")
	{
		channel.BasicPublish("logs.error.exchange", "error", props, body);
		channel.WaitForConfirmsOrDie(new TimeSpan(0, 0, 5));
	}
	else
	{
		channel.BasicPublish("logs.info.exchange", "", props, body);
	}

	Console.WriteLine($"Published {log.Level}: {log.Message}");
	Thread.Sleep(500);
}