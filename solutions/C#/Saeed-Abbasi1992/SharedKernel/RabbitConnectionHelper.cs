using RabbitMQ.Client;

namespace SharedKernel
{
    public class RabbitConnectionHelper
    {
        public async Task<IConnection> ConnectAsync(string amqpUri, int maxRetryCount, string clientName)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    var factory = new RabbitMQ.Client.ConnectionFactory
                    {
                        Uri = new Uri(amqpUri),
                        AutomaticRecoveryEnabled = true,
                        TopologyRecoveryEnabled = true
                    };

                    var connection = await factory.CreateConnectionAsync();
                    ConsoleLogger.LogInfo($"{clientName} connected to RabbitMQ at {amqpUri} (attempt {attempt})");

                    return connection;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    ConsoleLogger.LogWarning($"Attempt {attempt}/{maxRetryCount} failed: {ex.Message}");

                    if (attempt != maxRetryCount)
                        await Task.Delay(1000);
                }
            }

            throw new Exception($"[Fatal] Could not connect after {maxRetryCount} attempts", lastException);
        }
    }
}
