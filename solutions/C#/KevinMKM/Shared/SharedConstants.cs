namespace Shared
{
    public static class SharedConstants
    {
        #region Rabbit MQ

        public const string AmqpUriEnv = "AMQP_URI";

        #endregion

        #region Error Flow

        public const string ErrorExchange = "logs.error.exchange";
        public const string ErrorQueue = "logs.error.q";
        public const string ErrorDLXExchange = "logs.error.dlx.exchange";
        public const string ErrorDLXQueue = "logs.error.dlx.q";

        #endregion

        #region Info Flow

        public const string InfoExchange = "logs.info.exchange";
        public const int PrefetchCount = 2;

        #endregion
    }
}