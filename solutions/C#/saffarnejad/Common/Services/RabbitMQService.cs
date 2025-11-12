using Common.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Common.Services
{
    public class RabbitMQService : IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly RabbitMQConfig _config;
        private readonly ILogger _logger;
        private readonly int _maxRetries = 5;
        private readonly TimeSpan _initialRetryDelay = TimeSpan.FromSeconds(1);

        public RabbitMQService(RabbitMQConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public IModel GetChannel()
        {
            if (_channel?.IsOpen == true)
                return _channel;

            ConnectWithRetry();
            return _channel!;
        }

        private void ConnectWithRetry()
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = _config.Host,
                        UserName = _config.User,
                        Password = _config.Password,
                        Port = _config.Port,
                        VirtualHost = _config.VirtualHost,
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    _logger.LogInformation("Successfully connected to RabbitMQ");
                    return;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt}/{MaxRetries} to connect to RabbitMQ failed", attempt + 1, _maxRetries);

                    if (attempt == _maxRetries - 1)
                        throw;

                    Thread.Sleep(_initialRetryDelay * (int)Math.Pow(2, attempt));
                }
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}