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
            var newChannel = await connection.CreateChannelAsync();

            await newChannel.ExchangeDeclareAsync(
                Constants.InfoExchangeName,
                ExchangeType.Fanout,
                durable: true,
                autoDelete: false
            );

            await newChannel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            await newChannel.QueueBindAsync(queueName, Constants.InfoExchangeName, "");

            return newChannel;
        }
    }
}