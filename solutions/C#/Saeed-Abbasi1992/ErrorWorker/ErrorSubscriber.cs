using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;
using System.Text;
using RabbitMqConstants = SharedKernel.Constants;

namespace ErrorWorker
{
    public class ErrorSubscriber : ISubscriber<ErrorMessageModel>
    {
        private IChannel? channel;
        private readonly IConnection connection;
        private readonly string workerId;

        public ErrorSubscriber(IConnection connection, string workerId)
        {
            this.connection = connection;
            this.workerId = workerId;
        }

        public async Task SubscribeAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            channel ??= await CreateAndConfigureChannelAsync();

            ConsoleLogger.LogInfo("Waiting for messages...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        ConsoleLogger.LogWarning("Received empty message");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                        return;
                    }

                    var error = System.Text.Json.JsonSerializer.Deserialize<ErrorMessageModel>(json);

                    if (error is null)
                    {
                        ConsoleLogger.LogWarning("Failed to deserialize ErrorMessageModel");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                        return;
                    }

                    ConsoleLogger.LogSuccess($"{workerId} {error.Id} Received severity={error.SeverityType}");

                    ConsoleLogger.LogInfo($"{workerId} {error.Id} Processing");

                    //simulating process
                    await Task.Delay(1000, cancellationToken);

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    ConsoleLogger.LogSuccess($"{workerId} {error.Id} Acked");
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Exception: {ex.Message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync(queue: RabbitMqConstants.ErrorQueueName, autoAck: false, consumer: consumer);
        }

        private async Task<IChannel> CreateAndConfigureChannelAsync()
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, false);

            var result = await connection.CreateChannelAsync(options, CancellationToken.None);

            await result.ExchangeDeclareAsync(RabbitMqConstants.ErrorExchangeName, ExchangeType.Direct, durable: true);

            await result.QueueDeclareAsync(
                queue: RabbitMqConstants.ErrorQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            await result.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            return result;
        }
    }
}
