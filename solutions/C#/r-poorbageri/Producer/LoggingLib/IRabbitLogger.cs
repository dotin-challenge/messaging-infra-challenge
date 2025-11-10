namespace LoggingLib;

public interface IRabbitLogger
{
    void Log(string message, RabbitLogLevel level, Exception? ex = null);
}
