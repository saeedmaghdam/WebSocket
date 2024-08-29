namespace WebSocketServer.Events
{
    public record ShutdownEvent(Guid? ClientId);
}
