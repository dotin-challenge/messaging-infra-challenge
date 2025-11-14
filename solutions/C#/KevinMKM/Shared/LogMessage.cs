namespace Shared;

public record LogMessage(
    string Id,
    string Service,
    string Message,
    string Severity,
    LogType Type,
    DateTime Timestamp = default
)
{
    public LogMessage(LogType logType) : this(Guid.NewGuid().ToString(), "", "", "", logType, DateTime.UtcNow) { }
}