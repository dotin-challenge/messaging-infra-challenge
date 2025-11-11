namespace InfoSubscriberA.Models;

public class LogMessage
{
	public DateTime Timestamp { get; set; }
	public string Level { get; set; } // "Error" or "Info"
	public string Message { get; set; }
	public string Service { get; set; }
}
