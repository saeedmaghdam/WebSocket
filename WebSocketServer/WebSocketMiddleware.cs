namespace WebSocketServer
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebSocketPool _webSocketPool;

        public WebSocketMiddleware(RequestDelegate next, IWebSocketPool webSocketPool)
        {
            _next = next;
            _webSocketPool = webSocketPool;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
                await _webSocketPool.InitializeAsync(context);
            else
                await _next(context);
        }
    }
}
