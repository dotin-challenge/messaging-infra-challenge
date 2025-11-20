namespace SharedKernel
{
    public static class Constants
    {
        public const string ErrorExchangeName = "logs.error.exchange";
        public const string InfoExchangeName = "logs.info.exchange";

        public const string ErrorQueueName = "logs.error.q";
        public const string ErrorRoutingKey = "logs.error";

        public const string InfoQueuePrefixName = "logs.info.q.";

        public const int MaxRetryCount = 5;
    }
}