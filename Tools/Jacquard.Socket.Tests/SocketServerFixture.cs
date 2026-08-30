using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Jacquard.Socket.Tests;

public sealed class SocketServerFixture : IAsyncLifetime
{
    public string WebSocketUrl => $"ws://127.0.0.1:{_port}/v1/ws";
    public string HttpUrl => $"http://127.0.0.1:{_port}/v1/ws";
    public string Token { get; } = "socket-test-token";

    public async Task InitializeAsync()
    {
        _port = FindFreePort();
        var project = FindRepositoryFile("Tools", "Jacquard.Socket", "Jacquard.Socket.csproj");
        var root = Directory.GetParent(project)!.Parent!.Parent!.FullName;

        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --project {Quote(project)}",
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["JACQUARD_SOCKET_PORT"] = _port.ToString();
        start.Environment["JACQUARD_REMOTE_TOKEN"] = Token;

        _process = Process.Start(start) ?? throw new InvalidOperationException("could not start socket server");
        _stdout = DrainAsync(_process.StandardOutput);
        _stderr = DrainAsync(_process.StandardError);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            if (_process.HasExited)
                throw new InvalidOperationException("socket server exited before listening\n" + await Logs());

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port, timeout.Token);
                return;
            }
            catch (SocketException) { }
            catch (OperationCanceledException) { break; }

            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException("socket server did not start\n" + await Logs());
    }

    public async Task DisposeAsync()
    {
        if (_process == null) return;

        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (InvalidOperationException) { }
        catch (TimeoutException) { }
        finally
        {
            _process.Dispose();
            if (_stdout != null) await _stdout;
            if (_stderr != null) await _stderr;
        }
    }

    async Task<string> Logs()
    {
        var stdout = _stdout == null ? "" : await _stdout;
        var stderr = _stderr == null ? "" : await _stderr;
        return stdout + stderr;
    }

    static async Task<string> DrainAsync(StreamReader reader)
    {
        var builder = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
            builder.AppendLine(line);
        return builder.ToString();
    }

    static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("could not locate Jacquard.Socket.csproj");
    }

    static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";

    int _port;
    Process? _process;
    Task<string>? _stdout;
    Task<string>? _stderr;
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SocketCollection : ICollectionFixture<SocketServerFixture>
{
    public const string Name = "Jacquard socket server";
}

static class WebSocketTestExtensions
{
    public static async Task<JsonDocument> ReceiveJsonAsync(this ClientWebSocket socket,
                                                             TimeSpan? timeout = null)
    {
        using var cancel = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        var buffer = new byte[8192];
        using var message = new MemoryStream();

        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, cancel.Token);
            if (received.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("peer closed the connection");

            Assert.Equal(WebSocketMessageType.Text, received.MessageType);
            message.Write(buffer, 0, received.Count);
            if (received.EndOfMessage)
                return JsonDocument.Parse(message.ToArray());
        }
    }

    public static async Task SendJsonAsync(this ClientWebSocket socket, object value,
                                           CancellationToken cancel = default)
    {
        var json = JsonSerializer.Serialize(value);
        await socket.SendTextAsync(json, cancel);
    }

    public static async Task SendTextAsync(this ClientWebSocket socket, string json,
                                           CancellationToken cancel = default)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancel);
    }

    public static async Task<JsonDocument> HelloAsync(this ClientWebSocket socket,
                                                       string role, string token)
    {
        await socket.SendJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "hello-" + Guid.NewGuid().ToString("N"),
            method = "session.hello",
            @params = new { protocolVersion = 1, role, token }
        });
        return await socket.ReceiveJsonAsync();
    }

    public static async Task<ClientWebSocket> ConnectAsync(this SocketServerFixture server)
    {
        var socket = new ClientWebSocket();
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await socket.ConnectAsync(new Uri(server.WebSocketUrl), cancel.Token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static async Task CloseAsync(this ClientWebSocket socket)
    {
        if (socket.State == WebSocketState.Open)
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test", cancel.Token); }
            catch (WebSocketException) { }
            catch (OperationCanceledException) { }
        }

        socket.Dispose();
    }
}
