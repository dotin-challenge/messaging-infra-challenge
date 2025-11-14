using RabbitMQ.Client;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;
using System.Text;
using Constants = SharedKernel.Constants;

namespace Producer
{
    public class InfoPublisher : IPublisher<InfoMessageModel>
    {
        private IChannel? channel;
        private readonly IConnection connection;

        public InfoPublisher(IConnection connection)
        {
            this.connection = connection;
        }

        public async Task PublishAsync(InfoMessageModel item, CancellationToken cancellationToken)
        {
            var properties = new BasicProperties { Persistent = true };

            var json = System.Text.Json.JsonSerializer.Serialize(item);

            var body = Encoding.UTF8.GetBytes(json);

            try
            {
                channel ??= await CreateAndConfigureChannelAsync();

                await channel.BasicPublishAsync(
                    exchange: Constants.InfoExchangeName,
                    routingKey: string.Empty,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                ConsoleLogger.LogSuccess($"[Producer] Sent Info id={item.Id} service={item.Service} msg=\"{item.Message}\" latency={item.Latency}ms");
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogWarning($"[Producer][InfoPublish] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task<IChannel> CreateAndConfigureChannelAsync()
        {
            var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(Constants.InfoExchangeName, ExchangeType.Fanout,
                durable: true, autoDelete: false);

            return channel;
        }
    }
}