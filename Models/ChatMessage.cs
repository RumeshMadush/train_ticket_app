namespace TrainTicketApp.Models;

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "bot"
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
