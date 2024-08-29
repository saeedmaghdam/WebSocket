namespace WebSocketServer.Events
{
    public record ConnectionLostEvent(Guid ClientId);
}
