using System.Text.Json.Serialization;

namespace WebSocketServer.Models
{
    public class Message
    {
        [JsonPropertyName("refId")]
        public Guid RefId { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }
}
