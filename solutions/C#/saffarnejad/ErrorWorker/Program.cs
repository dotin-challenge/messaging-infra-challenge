using Common.Models;
using Common.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErrorWorker
{
    internal class Program
    {
        private static ILogger<Program> _logger;
        private static RabbitMQService _rabbitMQService;
        private static IModel _channel;
        private static readonly string ErrorExchange = "logs.error.exchange";
        private static readonly string ErrorQueue = "logs.error.q";
        private static string _workerId = Guid.NewGuid().ToString()[..8];

        static async Task Main(string[] args)
        {
            Initialize();

            // Setup consumer for error messages
            SetupErrorConsumer();

            Console.WriteLine("Error Worker started. Press [enter] to exit.");
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

        private static void SetupErrorConsumer()
        {
            // Ensure queue exists
            _channel.QueueDeclare(ErrorQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Fair dispatch: prefetch count = 1
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);

                try
                {
                    var logMessage = System.Text.Json.JsonSerializer.Deserialize<LogMessage>(message);
                    if (logMessage != null)
                    {
                        _logger.LogInformation("[ErrorWorker-{WorkerId}] {MessageId} received from service={Service} - processing...",
                            _workerId, logMessage.Id, logMessage.Service);

                        // Simulate processing time based on severity
                        var processingTime = logMessage.Severity switch
                        {
                            "LOW" => 1000,
                            "MEDIUM" => 2000,
                            "HIGH" => 4000,
                            "CRITICAL" => 6000,
                            _ => 3000
                        };

                        await Task.Delay(processingTime);

                        _logger.LogInformation("[ErrorWorker-{WorkerId}] {MessageId} processed successfully - acked",
                            _workerId, logMessage.Id);

                        // Manual acknowledgment
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize message: {Message}", message);
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {DeliveryTag}", ea.DeliveryTag);
                    // Negative acknowledgment with requeue
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(queue: ErrorQueue,
                autoAck: false,
                consumer: consumer);

            _logger.LogInformation("Error Worker {WorkerId} started and consuming from {Queue}", _workerId, ErrorQueue);
        }

        private static void Cleanup()
        {
            _rabbitMQService?.Dispose();
        }
    }
}