using RabbitMQ.Client;

namespace Shared
{
    public class RabbitConnection : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitConnection(string uri)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _channel.BasicQos(0, SharedConstants.PrefetchCount, false);
            
            _channel.ExchangeDeclare(SharedConstants.ErrorDLXExchange, ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(SharedConstants.ErrorDLXQueue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(SharedConstants.ErrorDLXQueue, SharedConstants.ErrorDLXExchange, "");
            
            _channel.QueueDeclare(SharedConstants.ErrorQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object> { { "x-dead-letter-exchange", SharedConstants.ErrorDLXExchange } }
            );
            _channel.ExchangeDeclare(SharedConstants.ErrorExchange, ExchangeType.Direct, durable: true);
            _channel.QueueBind(SharedConstants.ErrorQueue, SharedConstants.ErrorExchange, "");
            
            _channel.ExchangeDeclare(SharedConstants.InfoExchange, ExchangeType.Fanout, durable: true);
            
            _channel.ConfirmSelect();
        }

        public IModel Channel => _channel;

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}