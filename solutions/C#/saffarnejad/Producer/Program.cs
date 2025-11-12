using Common.Models;
using Common.Services;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Producer
{
    internal class Program
    {
        private static ILogger<Program> _logger;
        private static RabbitMQService _rabbitMQService;
        private static IModel _channel;
        private static readonly string ErrorExchange = "logs.error.exchange";
        private static readonly string InfoExchange = "logs.info.exchange";
        private static readonly string ErrorQueue = "logs.error.q";

        static void Main(string[] args)
        {
            Initialize();

            // Setup exchanges and queues
            SetupInfrastructure();

            // Start producing messages
            ProduceMessages();

            Console.WriteLine("Press [enter] to exit.");
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

        private static void SetupInfrastructure()
        {
            // Error exchange (Direct) and queue
            _channel.ExchangeDeclare(ErrorExchange, ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(ErrorQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            _channel.QueueBind(ErrorQueue, ErrorExchange, routingKey: "");

            // Info exchange (Fanout)
            _channel.ExchangeDeclare(InfoExchange, ExchangeType.Fanout, durable: true);

            _logger.LogInformation("RabbitMQ infrastructure setup completed");
        }

        private static void ProduceMessages()
        {
            var random = new Random();
            var services = new[] { "auth", "web", "api", "database", "cache" };
            var errorMessages = new[]
            {
                "DB timeout",
                "Connection refused",
                "Authentication failed",
                "Resource not found",
                "Internal server error"
            };

            var infoMessages = new[]
            {
                "GET /api/orders 200",
                "POST /api/users 201",
                "PUT /api/products 200",
                "DELETE /api/orders 204",
                "GET /api/health 200"
            };

            var severities = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };

            _ = Task.Run(async () =>
            {
                var messageId = 1;
                while (true)
                {
                    try
                    {
                        // Randomly choose between Error and Info messages
                        if (random.Next(0, 2) == 0)
                        {
                            // Send Error message
                            var errorLog = new LogMessage
                            {
                                Id = $"E-{messageId++:0000}",
                                Type = "Error",
                                Service = services[random.Next(services.Length)],
                                Message = errorMessages[random.Next(errorMessages.Length)],
                                Severity = severities[random.Next(severities.Length)]
                            };

                            var body = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(errorLog));
                            var properties = _channel.CreateBasicProperties();
                            properties.Persistent = true;
                            properties.MessageId = errorLog.Id;

                            _channel.BasicPublish(
                                exchange: ErrorExchange,
                                routingKey: "",
                                mandatory: true,
                                basicProperties: properties,
                                body: body);

                            _logger.LogInformation("[Producer] Sent Error id={Id} service={Service} msg=\"{Message}\" severity={Severity}",
                                errorLog.Id, errorLog.Service, errorLog.Message, errorLog.Severity);
                        }
                        else
                        {
                            // Send Info message
                            var infoLog = new LogMessage
                            {
                                Id = $"I-{messageId++:0000}",
                                Type = "Info",
                                Service = services[random.Next(services.Length)],
                                Message = infoMessages[random.Next(infoMessages.Length)],
                                LatencyMs = random.Next(10, 500)
                            };

                            var body = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(infoLog));
                            var properties = _channel.CreateBasicProperties();
                            properties.Persistent = true;
                            properties.MessageId = infoLog.Id;

                            _channel.BasicPublish(
                                exchange: InfoExchange,
                                routingKey: "",
                                mandatory: true,
                                basicProperties: properties,
                                body: body);

                            _logger.LogInformation("[Producer] Sent Info  id={Id} service={Service} msg=\"{Message}\" latency_ms={Latency}",
                                infoLog.Id, infoLog.Service, infoLog.Message, infoLog.LatencyMs);
                        }

                        await Task.Delay(2000); // Send message every 2 seconds
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while producing message");
                        await Task.Delay(5000); // Wait 5 seconds before retrying
                    }
                }
            });
        }

        private static void Cleanup()
        {
            _rabbitMQService?.Dispose();
        }
    }
}