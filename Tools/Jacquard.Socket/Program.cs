using Jacquard.Socket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

var port = Environment.GetEnvironmentVariable("JACQUARD_SOCKET_PORT") ?? "38271";
var builder = WebApplication.CreateBuilder(Array.Empty<string>());
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

var app = builder.Build();
var hub = new SessionHub(Environment.GetEnvironmentVariable("JACQUARD_REMOTE_TOKEN") ?? "");

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.Map("/v1/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.Handle(socket, context.RequestAborted);
});

Console.WriteLine($"Jacquard socket API listening on ws://127.0.0.1:{port}/v1/ws");
await app.RunAsync();
