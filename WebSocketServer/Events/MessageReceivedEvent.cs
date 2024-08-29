namespace WebSocketServer.Events
{
    public record MessageReceivedEvent(Guid ClientId, string Message);
}
