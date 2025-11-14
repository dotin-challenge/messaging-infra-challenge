using Shared;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Producer
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
            using var rabbit = new RabbitConnection(uri);
            var channel = rabbit.Channel;
            var random = new Random();
            var services = new[] { "auth", "web", "api", "db", "cache" };

            for (var i = 1; i <= 20; i++)
            {
                var typeRoll = random.Next(0, 100);
                if (typeRoll < 30)
                    await SendErrorAsync(channel, new LogMessage(Guid.NewGuid().ToString(),
                        services[random.Next(services.Length)],
                        "DB timeout", "HIGH", LogType.Error));
                else
                    await SendInfoAsync(channel, new LogMessage(Guid.NewGuid().ToString(),
                        services[random.Next(services.Length)],
                        "GET /api 200", "LOW", LogType.Info));

                await Task.Delay(random.Next(500, 1500));
            }
        }

        private static async Task SendErrorAsync(IModel channel, LogMessage msg)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;

            channel.BasicPublish(SharedConstants.ErrorExchange, "", props, body);
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            Console.WriteLine($"[Producer] Sent Error {msg.Id}");
        }

        private static async Task SendInfoAsync(IModel channel, LogMessage msg)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var props = channel.CreateBasicProperties();
            props.Persistent = false;

            var attempts = 0;
            while (attempts < 5)
            {
                try
                {
                    channel.BasicPublish(SharedConstants.InfoExchange, "", props, body);
                    Console.WriteLine($"[Producer] Sent Info {msg.Id}");
                    break;
                }
                catch
                {
                    attempts++;
                    var delayMs = (int)Math.Pow(2, attempts) * 1000;
                    Console.WriteLine($"[Producer] Retry Info {msg.Id}, attempt {attempts}, delay {delayMs}ms");
                    await Task.Delay(delayMs);
                }
            }
        }
    }
}