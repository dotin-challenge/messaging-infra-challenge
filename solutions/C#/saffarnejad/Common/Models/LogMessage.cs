namespace Common.Models
{
    public class LogMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Error" or "Info"
        public string Service { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public int? LatencyMs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}