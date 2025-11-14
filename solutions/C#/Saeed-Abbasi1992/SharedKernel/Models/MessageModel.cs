namespace SharedKernel.Models
{
    public class MessageModel
    {
        public string Id { get; set; }
        public required string Message { get; set; }
        public string Service { get; set; }
    }
}
