using Microsoft.AspNetCore.Mvc;
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

var webSocketPool = app.Services.GetRequiredService<IWebSocketPool>();
webSocketPool.ConnectionEstablished += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} connected.", e.ClientId);
webSocketPool.ConnectionLost += (sender, e) => app.Logger.LogInformation("EventHandler: Client {clientId} disconnected.", e.ClientId);
webSocketPool.Received += (sender, e) => app.Logger.LogInformation("EventHandler: Received message from client {clientId}: {message}", e.ClientId, e.Message);
webSocketPool.ConnectionAlreadyExist += (sender, e) => app.Logger.LogInformation("EventHandler: Failed to add client {clientId}.", e.ClientId);
webSocketPool.Shutdown += (sender, e) => app.Logger.LogInformation(!e.ClientId.HasValue ? "EventHandler: Server shutdown.": "EventHandler: Client {clientId} shutdown.", e.ClientId);

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