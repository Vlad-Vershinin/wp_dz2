using System.Text.Json.Serialization;

namespace client.Models;

public class Message
{
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = "Aboba";
    public DateTime Time { get; set; } = DateTime.UtcNow;
    [JsonIgnore]
    public bool IsOwnMessage { get; set; }
}
