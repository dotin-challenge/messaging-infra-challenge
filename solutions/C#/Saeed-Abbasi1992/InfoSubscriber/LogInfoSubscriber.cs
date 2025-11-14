using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;
using System.Text;
using Constants = SharedKernel.Constants;

namespace InfoSubscriber
{
    public class LogInfoSubscriber : ISubscriber<InfoMessageModel>
    {
        private IChannel? channel;
        private readonly IConnection connection;
        private readonly string serviceName;
        private readonly string queueName;

        public LogInfoSubscriber(IConnection connection, string serviceName)
        {
            this.connection = connection;
            this.serviceName = $"[InfoSub-{serviceName}]";
            this.queueName = $"{Constants.InfoQueuePrefixName}{serviceName}";
        }

        public async Task SubscribeAsync(CancellationToken cancellationToken)
        {
            channel ??= await CreateAndConfigureChannelAsync();

            ConsoleLogger.LogInfo($"{serviceName} Waiting for messages...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        ConsoleLogger.LogWarning($"{serviceName} Received empty message");
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false);
                        return;
                    }

                    var info = System.Text.Json.JsonSerializer.Deserialize<InfoMessageModel>(json);

                    if (info is null)
                    {
                        ConsoleLogger.LogWarning($"{serviceName} Failed to deserialize InfoMessageModel");
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false);
                        return;
                    }

                    ConsoleLogger.LogSuccess($"{serviceName} {info.Id} -> dashboard updated (latency={info.Latency}ms)");

                    await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"{serviceName} Exception: {ex.Message}");
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);
        }

        private async Task<IChannel> CreateAndConfigureChannelAsync()
        {
            var result = await connection.CreateChannelAsync();

            // Declare main fanout exchange (required!)
            await result.ExchangeDeclareAsync(
                Constants.InfoExchangeName,
                ExchangeType.Fanout,
                durable: true
            );

            // Dead Letter Exchange
            var deadLetterExchangeName = $"{Constants.InfoExchangeName}.dlx";
            await result.ExchangeDeclareAsync(deadLetterExchangeName, ExchangeType.Fanout, durable: true);

            var queueArgs = new Dictionary<string, object?> { { "x-dead-letter-exchange", deadLetterExchangeName } };

            // Main queue
            await result.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs
            );

            // Dead Letter Queue
            var deadLetterQueueName = $"{queueName}.dlq";
            await result.QueueDeclareAsync(
                queue: deadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            await result.QueueBindAsync(deadLetterQueueName, deadLetterExchangeName, "");

            // Bind main queue to main exchange
            await result.QueueBindAsync(queueName, Constants.InfoExchangeName, "");

            return result;
        }
    }
}
