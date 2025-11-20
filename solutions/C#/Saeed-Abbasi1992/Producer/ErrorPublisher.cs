using RabbitMQ.Client;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;
using System.Text;
using Constants = SharedKernel.Constants;


public class ErrorPublisher : IPublisher<ErrorMessageModel>
{
    private readonly IConnection connection;
    IChannel? channel;

    public ErrorPublisher(IConnection connection)
    {
        this.connection = connection;
    }

    public async Task PublishAsync(ErrorMessageModel item, CancellationToken cancellationToken)
    {
        int maxRetryCount = Constants.MaxRetryCount;

        for (int attempt = 1; attempt <= maxRetryCount; attempt++)
        {
            try
            {
                channel ??= await CreateAndConfigureChannelAsync(cancellationToken);

                if (channel.IsClosed)
                    channel = await CreateAndConfigureChannelAsync(cancellationToken);

                var properties = new BasicProperties
                {
                    Persistent = true
                };

                var json = System.Text.Json.JsonSerializer.Serialize(item);
                var body = Encoding.UTF8.GetBytes(json);

                await channel.BasicPublishAsync(
                    exchange: Constants.ErrorExchangeName,
                    routingKey: Constants.ErrorRoutingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                ConsoleLogger.LogSuccess($"[Producer] Sent Error id={item.Id} service={item.Service} msg=\"{item.Message}\" severity={item.SeverityType}");

                return;
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogWarning($"Publish failed ({attempt}/{maxRetryCount}): {ex.Message}");

                if (attempt < maxRetryCount)
                    await Task.Delay(500, cancellationToken);

                channel = null;
            }
        }

        throw new Exception($"[Fatal] ErrorPublisher failed after {maxRetryCount} retries.");
    }

    private async Task<IChannel> CreateAndConfigureChannelAsync(CancellationToken cancellationToken)
    {
        var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, false);
        var newChannel = await connection.CreateChannelAsync(options, cancellationToken);

        // Main Exchange
        await newChannel.ExchangeDeclareAsync(Constants.ErrorExchangeName, ExchangeType.Direct, durable: true);

        // Dead-Letter Exchange و Queue
        var dlxName = $"{Constants.ErrorExchangeName}.dlx";
        await newChannel.ExchangeDeclareAsync(dlxName, ExchangeType.Direct, durable: true);
        await newChannel.QueueDeclareAsync("logs.error.dlq", durable: true, exclusive: false, autoDelete: false);
        await newChannel.QueueBindAsync("logs.error.dlq", dlxName, "");

        var queueArgs = new Dictionary<string, object?> { { "x-dead-letter-exchange", dlxName } };
        await newChannel.QueueDeclareAsync(
            Constants.ErrorQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);

        await newChannel.QueueBindAsync(Constants.ErrorQueueName, Constants.ErrorExchangeName, Constants.ErrorRoutingKey);

        return newChannel;
    }
}

