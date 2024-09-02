using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using WebSocketServer;
using WebSocketServer.Models;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IWebSocketPool, WebSocketPool>();

var app = builder.Build();

_ = bool.TryParse(app.Configuration["RabbitMQ:Enabled"], out bool isRabbitMqEnabled);
var exchangeName = app.Configuration["RabbitMQ:ExchangeName"];
var channel = default(IModel);

if (isRabbitMqEnabled)
{
    var factory = new ConnectionFactory { Uri = new Uri(app.Configuration["ConnectionStrings:RabbitMQ"]!) };
    var connection = factory.CreateConnection();
    channel = connection.CreateModel();
}

var webSocketPool = app.Services.GetRequiredService<IWebSocketPool>();
webSocketPool.ConnectionEstablished += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} connected.", e.ClientId);
webSocketPool.ConnectionLost += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} disconnected.", e.ClientId);
webSocketPool.Received += (sender, e) =>
{
    app.Logger.LogInformation("EventHandler: Received message from client {clientId}: {message}", e.ClientId, e.Message);

    if (isRabbitMqEnabled)
    {
        var message = JsonSerializer.Deserialize<Message>(e.Message);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        channel.BasicPublish(exchange: exchangeName,
                             routingKey: e.ClientId.ToString(),
                             basicProperties: null,
                             body: body);
    }
};
webSocketPool.ConnectionAlreadyExist += (sender, e) => app.Logger.LogInformation("EventHandler: Failed to add client {clientId}.", e.ClientId);
webSocketPool.Shutdown += (sender, e) => app.Logger.LogInformation(!e.ClientId.HasValue ? "EventHandler: Server shutdown." : "EventHandler: Client {clientId} shutdown.", e.ClientId);

app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/SendMessage/{clientId}", async ([FromRoute] Guid clientId, [FromBody] Message message, [FromServices] IWebSocketPool webSocketPool) =>
{
    await webSocketPool.SendAsync(clientId, JsonSerializer.Serialize(message));
})
.WithName("SendMessage")
.WithOpenApi();

app.Run();