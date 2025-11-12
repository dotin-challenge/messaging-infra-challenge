namespace Common.Models
{
    public class RabbitMQConfig
    {
        public string Host { get; set; } = "localhost";
        public string User { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
    }
}