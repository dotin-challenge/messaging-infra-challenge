using Common.Models;
using Common.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InfoSubscriber
{
    internal class Program
    {
        private static ILogger<Program> _logger;
        private static RabbitMQService _rabbitMQService;
        private static IModel _channel;
        private static readonly string InfoExchange = "logs.info.exchange";
        private static string _serviceName = string.Empty;

        static async Task Main(string[] args)
        {
            _serviceName = args.Length > 0 ? args[0] : "default";

            Initialize();

            // Setup consumer for info messages
            SetupInfoConsumer();

            Console.WriteLine($"Info Subscriber '{_serviceName}' started. Press [enter] to exit.");
            Console.ReadLine();

            Cleanup();
        }

        private static void Initialize()
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            _logger = loggerFactory.CreateLogger<Program>();

            // Get RabbitMQ configuration
            var rabbitConfig = new RabbitMQConfig();
            configuration.GetSection("RabbitMQ").Bind(rabbitConfig);

            // Override with environment variables if present
            var amqpUri = Environment.GetEnvironmentVariable("AMQP_URI");
            if (!string.IsNullOrEmpty(amqpUri))
            {
                var uri = new Uri(amqpUri);
                rabbitConfig.Host = uri.Host;
                rabbitConfig.Port = uri.Port;
                rabbitConfig.User = uri.UserInfo.Split(':')[0];
                rabbitConfig.Password = uri.UserInfo.Split(':')[1];
                rabbitConfig.VirtualHost = uri.AbsolutePath.TrimStart('/');
            }

            _rabbitMQService = new RabbitMQService(rabbitConfig, _logger);
            _channel = _rabbitMQService.GetChannel();
        }

        private static void SetupInfoConsumer()
        {
            // Ensure exchange exists
            _channel.ExchangeDeclare(InfoExchange, ExchangeType.Fanout, durable: true);

            // Create a unique queue for this subscriber service
            var queueName = $"logs.info.q.{_serviceName}";

            var queueDeclareResult = _channel.QueueDeclare(queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueBind(queue: queueName,
                exchange: InfoExchange,
                routingKey: "");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);

                try
                {
                    var logMessage = System.Text.Json.JsonSerializer.Deserialize<LogMessage>(message);
                    if (logMessage != null)
                    {
                        // Simulate different processing based on service type
                        var action = _serviceName.ToLower() switch
                        {
                            "grafana" => "dashboard updated",
                            "elk" => "indexed in elasticsearch",
                            "splunk" => "added to splunk index",
                            "datadog" => "sent to datadog metrics",
                            _ => "processed"
                        };

                        _logger.LogInformation("[InfoSub-{ServiceName}] {MessageId} -> {Action} (latency: {Latency}ms)",
                            _serviceName, logMessage.Id, action, logMessage.LatencyMs);

                        // Auto-acknowledge for info messages (fire-and-forget)
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize info message");
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing info message");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: queueName,
                autoAck: false,
                consumer: consumer);

            _logger.LogInformation("Info Subscriber '{ServiceName}' started and consuming from queue '{Queue}'",
                _serviceName, queueName);
        }

        private static void Cleanup()
        {
            _rabbitMQService?.Dispose();
        }
    }
}