using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using WebSocketServer.Events;

namespace WebSocketServer
{
    public interface IWebSocketPool
    {
        event EventHandler<ConnectionEstablishedEvent> ConnectionEstablished;
        event EventHandler<ConnectionLostEvent> ConnectionLost;
        event EventHandler<MessageReceivedEvent> Received;
        event EventHandler<ConnectionAlreadyExistEvent> ConnectionAlreadyExist;
        event EventHandler<ShutdownEvent> Shutdown;

        Task InitializeAsync(HttpContext context);
        Task SendAsync(Guid clientId, string message);
        Task ShutdownAsync();
        Task ShutdownAsync(Guid clientId);
    }

    public class WebSocketPool : IWebSocketPool
    {
        public event EventHandler<ConnectionEstablishedEvent> ConnectionEstablished;
        public event EventHandler<ConnectionLostEvent> ConnectionLost;
        public event EventHandler<MessageReceivedEvent> Received;
        public event EventHandler<ConnectionAlreadyExistEvent> ConnectionAlreadyExist;
        public event EventHandler<ShutdownEvent> Shutdown;

        private readonly ILogger<WebSocketPool> _logger;

        private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new ConcurrentDictionary<Guid, WebSocket>();

        private readonly bool _loggingEnabled;

        public WebSocketPool(ILogger<WebSocketPool> logger, IConfiguration configuration)
        {
            _logger = logger;
            _loggingEnabled = bool.TryParse(configuration["Logging:Enabled"], out bool enabled) && enabled;
        }

        public async Task InitializeAsync(HttpContext context)
        {
            if (Guid.TryParse(context.Request.Query["clientId"], out Guid clientId))
            {
                using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                {
                    if (_clients.TryAdd(clientId, webSocket))
                    {
                        if (_loggingEnabled) _logger.LogInformation("Client {clientId} connected.", clientId);
                        ConnectionEstablished?.Invoke(this, new(clientId));

                        await HandleWebSocket(clientId, webSocket);
                    }
                    else
                    {
                        await CloseConnectionAsync(webSocket, clientId, () => ConnectionAlreadyExist?.Invoke(this, new(clientId)));
                    }
                }
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid client ID");
            }
        }

        public async Task SendAsync(Guid clientId, string message)
        {
            if (_clients.TryGetValue(clientId, out WebSocket webSocket) && webSocket.State == WebSocketState.Open)
            {
                var serverMsg = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(serverMsg, 0, serverMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                if (_loggingEnabled) _logger.LogWarning("No active WebSocket connection for client {clientId}. Unable to send message.", clientId);
            }
        }

        public async Task ShutdownAsync()
        {
            foreach (var client in _clients)
                await CloseConnectionAsync(client.Value, client.Key);

            Shutdown?.Invoke(this, new(default));
        }

        public async Task ShutdownAsync(Guid clientId)
        {
            if (!_clients.TryGetValue(clientId, out WebSocket webSocket))
                return;

            await CloseConnectionAsync(webSocket, clientId, () => Shutdown?.Invoke(this, new(clientId)));
        }

        private async Task HandleWebSocket(Guid clientId, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (_loggingEnabled) _logger.LogInformation("Received message from client {clientId}.", clientId);
                    Received?.Invoke(this, new(clientId, message));

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_loggingEnabled) _logger.LogError(ex, "WebSocket Error with client {clientId}", clientId);
            }
            finally
            {
                await CloseConnectionAsync(webSocket, clientId, () => ConnectionLost?.Invoke(this, new(clientId)));
            }
        }

        private async Task CloseConnectionAsync(WebSocket webSocket, Guid clientId, Action? callback = default)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            _ = _clients.TryRemove(clientId, out _);

            if (_loggingEnabled) _logger.LogInformation("Client {clientId} disconnected.", clientId);

            if (callback is not null)
                callback();
        }
    }
}
