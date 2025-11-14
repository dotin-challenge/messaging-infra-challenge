namespace SharedKernel
{
    public class AMQPUriHelper
    {
        public static string GetAMQP_URI(string? amqpUri, string? host, string? user, string? password)
        {
            if (!string.IsNullOrWhiteSpace(amqpUri))
                return amqpUri;

            host ??= "localhost";
            user ??= "guest";
            password ??= "guest";

            return $"amqp://{user}:{password}@{host}:5672/";
        }

        public static string GetAMQP_URI()
        {
            return GetAMQP_URI(
                    Environment.GetEnvironmentVariable("AMQP_URI"),
                    Environment.GetEnvironmentVariable("RABBIT_HOST"),
                    Environment.GetEnvironmentVariable("RABBIT_USER"),
                    Environment.GetEnvironmentVariable("RABBIT_PASS"));
        }
    }
}
