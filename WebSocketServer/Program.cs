using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using WebSocketServer;
using WebSocketServer.Models;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

builder.Configuration.AddConfiguration(configuration);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IWebSocketPool, WebSocketPool>();

var app = builder.Build();

_ = bool.TryParse(app.Configuration["RABBITMQ:ENABLED"], out bool isRabbitMqEnabled);
var exchangeName = app.Configuration["RABBITMQ:EXCHANGENAME"];
var channel = default(IModel);

if (isRabbitMqEnabled)
{
    var factory = new ConnectionFactory { Uri = new Uri(app.Configuration["CONNECTIONSTRINGS:RABBITMQ"]!) };
    var connection = factory.CreateConnection();
    channel = connection.CreateModel();

    channel.ExchangeDeclare(exchange: exchangeName, type: ExchangeType.Direct);
}

var webSocketPool = app.Services.GetRequiredService<IWebSocketPool>();
webSocketPool.ConnectionEstablished += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} connected.", e.ClientId);
webSocketPool.ConnectionLost += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} disconnected.", e.ClientId);
webSocketPool.Received += (sender, e) =>
{
    app.Logger.LogInformation("EventHandler: Received message from client {clientId}: {message}", e.ClientId, e.Message);

    if (isRabbitMqEnabled)
    {
        var message = e.Message;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        channel.BasicPublish(exchange: exchangeName,
                             routingKey: e.ClientId.ToString(),
                             basicProperties: null,
                             body: body);
    }
};
webSocketPool.ConnectionAlreadyExist += (sender, e) => app.Logger.LogInformation("EventHandler: Failed to add client {clientId}.", e.ClientId);
webSocketPool.Shutdown += (sender, e) => app.Logger.LogInformation(!e.ClientId.HasValue ? "EventHandler: Server shutdown." : "EventHandler: Client {clientId} shutdown.", e.ClientId);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = Timeout.InfiniteTimeSpan
});
app.UseMiddleware<WebSocketMiddleware>();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost("/SendMessage/{clientId}", async ([FromRoute] Guid clientId, [FromBody] Message message, [FromServices] IWebSocketPool webSocketPool) =>
{
    await webSocketPool.SendAsync(clientId, JsonSerializer.Serialize(message));
})
.WithName("SendMessage")
.WithOpenApi();

app.Run();