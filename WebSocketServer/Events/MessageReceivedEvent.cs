using WebSocketServer.Models;

namespace WebSocketServer.Events
{
    public record MessageReceivedEvent(Guid ClientId, Message Message);
}
