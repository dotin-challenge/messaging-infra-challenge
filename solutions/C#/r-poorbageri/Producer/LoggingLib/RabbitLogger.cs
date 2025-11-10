using RabbitMQ.Client;
using System.Text;

namespace LoggingLib
{
    public class RabbitLogger : IRabbitLogger, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _serviceName;
        private readonly string _errorExchange;
        private readonly string _infoExchange;
        private readonly TimeSpan _confirmTimeout = TimeSpan.FromSeconds(5);

        public RabbitLogger(string host, string user, string pass, string serviceName,
                            string errorExchange = "logs.error.exchange",
                            string infoExchange = "logs.info.exchange")
        {
            _serviceName = serviceName;
            _errorExchange = errorExchange;
            _infoExchange = infoExchange;

            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = user,
                Password = pass,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(_errorExchange, ExchangeType.Direct, durable: true);
            _channel.ExchangeDeclare(_infoExchange, ExchangeType.Fanout, durable: true);

            _channel.ConfirmSelect();
        }

        public void Log(string message, RabbitLogLevel level, Exception? ex = null)
        {
            try
            {
                var props = BuildProperties(level, ex);
                var body = Encoding.UTF8.GetBytes(message);

                if (level == RabbitLogLevel.Error)
                    _channel.BasicPublish(_errorExchange, "error", props, body);
                else
                    _channel.BasicPublish(_infoExchange, "", props, body);

                if (!_channel.WaitForConfirms(_confirmTimeout))
                    throw new Exception("Publisher confirm timed out.");

                Console.WriteLine($"[{level}] Sent: {message}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Logger] Failed to publish: {e.Message}");
            }
        }

        private IBasicProperties BuildProperties(RabbitLogLevel level, Exception? ex)
        {
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = Guid.NewGuid().ToString();
            props.Headers = new Dictionary<string, object>
            {
                { "type", level.ToString().ToLowerInvariant() },
                { "created-at", DateTime.UtcNow.ToString("o") },
                { "trace-id", Guid.NewGuid().ToString() },
                { "origin-service", _serviceName },
                { "severity", level == RabbitLogLevel.Error ? "high" : "normal" }
            };

            if (ex != null)
            {
                props.Headers["exception-type"] = ex.GetType().Name;
                props.Headers["exception-message"] = ex.Message;
                props.Headers["stacktrace"] = ex.StackTrace ?? string.Empty;
            }

            return props;
        }

        public void Dispose()
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
        }

    }
}
