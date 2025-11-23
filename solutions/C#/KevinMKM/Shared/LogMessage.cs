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
    public LogMessage() : this(Guid.NewGuid().ToString(), "", "", "", LogType.Info, DateTime.UtcNow) { }
}