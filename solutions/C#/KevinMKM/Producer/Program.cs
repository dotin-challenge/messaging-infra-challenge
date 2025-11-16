using Shared;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Producer
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var uri = Environment.GetEnvironmentVariable(SharedConstants.AmqpUriEnv) ?? "amqp://guest:guest@localhost:5672/";
            var prefetch = int.TryParse(Environment.GetEnvironmentVariable(SharedConstants.PrefetchEnv), out var p) ? p : 2;

            using var rabbit = new RabbitConnection(uri, prefetch);
            var channel = rabbit.Channel;

            var random = new Random();
            var services = new[] { "auth", "web", "api", "db", "cache" };

            for (int i = 0; i < 50; i++)
            {
                var typeRoll = random.Next(0, 100);
                if (typeRoll < 30)
                {
                    var msg = new LogMessage(Guid.NewGuid().ToString(),
                        services[random.Next(services.Length)],
                        "DB timeout", "HIGH", LogType.Error);
                    await SendErrorAsync(channel, msg);
                }
                else
                {
                    var msg = new LogMessage(Guid.NewGuid().ToString(),
                        services[random.Next(services.Length)],
                        "GET /api 200", "LOW", LogType.Info);
                    await SendInfoAsync(channel, msg);
                }

                await Task.Delay(random.Next(200, 800));
            }

            return 0;
        }

        private static async Task SendErrorAsync(IModel channel, LogMessage msg)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;

            var attempts = 0;
            while (attempts < 5)
            {
                try
                {
                    channel.BasicPublish(SharedConstants.ErrorExchange, "", props, body);
                    // Wait for confirm (timeout small)
                    if (!channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                    {
                        throw new Exception("Publish not confirmed");
                    }
                    Console.WriteLine($"[Producer] Sent Error {msg.Id}");
                    break;
                }
                catch (Exception ex)
                {
                    attempts++;
                    var delayMs = (int)Math.Pow(2, attempts) * 500;
                    Console.WriteLine($"[Producer] Error publish failed ({attempts}). Retrying in {delayMs}ms. Err: {ex.Message}");
                    await Task.Delay(delayMs);
                }
            }
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
                catch (Exception ex)
                {
                    attempts++;
                    var delayMs = (int)Math.Pow(2, attempts) * 300;
                    Console.WriteLine($"[Producer] Info publish failed ({attempts}). Retrying in {delayMs}ms. Err: {ex.Message}");
                    await Task.Delay(delayMs);
                }
            }
        }
    }
}