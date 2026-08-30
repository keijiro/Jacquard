using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Jacquard.Socket;

sealed class SessionHub(string token)
{
    public async Task Handle(WebSocket socket, CancellationToken cancel)
    {
        var peer = new Peer(socket);
        string? role = null;

        try
        {
            var hello = await peer.Receive(cancel);
            JsonDocument document;
            try { document = JsonDocument.Parse(hello); }
            catch (JsonException)
            {
                await peer.Send(Error("", "invalid_handshake", "session.hello must be valid JSON"), cancel);
                return;
            }

            using (document)
            {
            var root = document.RootElement;

            if (!Request(root, out var id, out var method) || method != "session.hello")
            {
                await peer.Send(Error(id, "invalid_handshake", "session.hello is required"), cancel);
                return;
            }

            if (!root.TryGetProperty("params", out var args) ||
                args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("protocolVersion", out var versionValue) ||
                versionValue.ValueKind != JsonValueKind.Number ||
                !versionValue.TryGetInt32(out var version) || version != 1 ||
                !args.TryGetProperty("role", out var roleValue) ||
                roleValue.ValueKind != JsonValueKind.String)
            {
                await peer.Send(Error(id, "invalid_handshake",
                                     "protocolVersion 1 and a string role are required"), cancel);
                return;
            }

            role = roleValue.GetString();
            var supplied = args.TryGetProperty("token", out var tokenValue) &&
                           tokenValue.ValueKind == JsonValueKind.String
                ? tokenValue.GetString() ?? "" : "";

            if (token.Length > 0 && supplied != token)
            {
                await peer.Send(Error(id, "unauthenticated", "invalid token"), cancel);
                return;
            }

            if (role == "runtime")
            {
                if (Interlocked.CompareExchange(ref _runtime, peer, null) != null)
                {
                    await peer.Send(Error(id, "runtime_exists", "a runtime is already connected"), cancel);
                    return;
                }
            }
            else if (role is "controller" or "observer")
                _controllers[peer.Id] = peer;
            else
            {
                await peer.Send(Error(id, "invalid_role", "role must be runtime, controller or observer"), cancel);
                return;
            }

            await peer.Send(Result(id, new
            {
                protocolVersion = 1,
                connectionId = peer.Id,
                runtimeConnected = _runtime != null
            }), cancel);

            if (role == "runtime") await RuntimeLoop(peer, cancel);
            else await ControllerLoop(peer, role == "observer", cancel);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (InvalidDataException)
        {
            await peer.Close(WebSocketCloseStatus.MessageTooBig, "message exceeds 1 MB");
        }
        finally
        {
            _controllers.TryRemove(peer.Id, out _);
            RemovePending(peer);

            if (role == "runtime" && Interlocked.CompareExchange(ref _runtime, null, peer) == peer)
            {
                foreach (var item in _pending.ToArray())
                    if (_pending.TryRemove(item.Key, out var controller))
                        await SafeSend(controller,
                                       Error(item.Key, "runtime_disconnected",
                                             "Jacquard runtime disconnected"),
                                       CancellationToken.None);

                await Broadcast(Notification("event.runtime.disconnected", new { }),
                                CancellationToken.None);
            }
        }
    }

    async Task RuntimeLoop(Peer runtime, CancellationToken cancel)
    {
        while (runtime.Open)
        {
            var json = await runtime.Receive(cancel);
            JsonDocument document;
            try { document = JsonDocument.Parse(json); }
            catch (JsonException)
            {
                await runtime.Close(WebSocketCloseStatus.ProtocolError, "runtime sent invalid JSON");
                return;
            }

            using (document)
            {
            var root = document.RootElement;

            if (!RpcMessage(root))
            {
                await runtime.Close(WebSocketCloseStatus.ProtocolError,
                                    "runtime messages must be JSON-RPC 2.0 objects");
                return;
            }

            if (root.TryGetProperty("id", out var idValue) &&
                idValue.ValueKind == JsonValueKind.String &&
                (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _)))
            {
                var id = idValue.GetString()!;
                if (_pending.TryRemove(id, out var controller))
                    await SafeSend(controller, json, cancel);
                continue;
            }

            if (root.TryGetProperty("method", out var methodValue) &&
                methodValue.ValueKind == JsonValueKind.String &&
                methodValue.GetString()?.StartsWith("event.", StringComparison.Ordinal) == true)
                await Broadcast(json, cancel);
            }
        }
    }

    async Task ControllerLoop(Peer controller, bool readOnly, CancellationToken cancel)
    {
        while (controller.Open)
        {
            var json = await controller.Receive(cancel);
            JsonDocument document;
            try { document = JsonDocument.Parse(json); }
            catch (JsonException)
            {
                await controller.Send(Error("", "invalid_json", "request must be valid JSON"), cancel);
                continue;
            }

            using (document)
            {
            var root = document.RootElement;

            if (!Request(root, out var id, out var method))
            {
                await controller.Send(Error(id, "invalid_request", "a string id and method are required"),
                                      cancel);
                continue;
            }

            if (readOnly && method is not "session.get" and not "project.get")
            {
                await controller.Send(Error(id, "read_only", "observer connections are read only"),
                                      cancel);
                continue;
            }

            var runtime = _runtime;
            if (runtime == null || !runtime.Open)
            {
                await controller.Send(Error(id, "runtime_not_connected",
                                            "Jacquard runtime is not connected"), cancel);
                continue;
            }

            if (!_pending.TryAdd(id, controller))
            {
                await controller.Send(Error(id, "duplicate_id", "request id is already in use"),
                                      cancel);
                continue;
            }

            try { await runtime.Send(json, cancel); }
            catch
            {
                _pending.TryRemove(id, out _);
                throw;
            }
            }
        }
    }

    async Task Broadcast(string json, CancellationToken cancel)
    {
        foreach (var controller in _controllers.Values)
            await SafeSend(controller, json, cancel);
    }

    void RemovePending(Peer peer)
    {
        foreach (var item in _pending.ToArray())
            if (ReferenceEquals(item.Value, peer)) _pending.TryRemove(item.Key, out _);
    }

    static async Task SafeSend(Peer peer, string json, CancellationToken cancel)
    {
        try { if (peer.Open) await peer.Send(json, cancel); }
        catch (Exception error) when (error is WebSocketException ||
                                      error is OperationCanceledException ||
                                      error is ObjectDisposedException) { }
    }

    static bool Request(JsonElement root, out string id, out string method)
    {
        id = RpcMessage(root) &&
             root.TryGetProperty("id", out var idValue) &&
             idValue.ValueKind == JsonValueKind.String ? idValue.GetString()! : "";
        method = RpcMessage(root) &&
                 root.TryGetProperty("method", out var methodValue) &&
                 methodValue.ValueKind == JsonValueKind.String ? methodValue.GetString()! : "";
        return id.Length > 0 && method.Length > 0;
    }

    static bool RpcMessage(JsonElement root)
      => root.ValueKind == JsonValueKind.Object &&
         root.TryGetProperty("jsonrpc", out var version) &&
         version.ValueKind == JsonValueKind.String && version.GetString() == "2.0";

    static string Result(string id, object result)
      => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });

    static string Error(string id, string kind, string message)
      => JsonSerializer.Serialize(new
      {
          jsonrpc = "2.0",
          id,
          error = new { code = -32000, message, data = new { kind } }
      });

    static string Notification(string method, object parameters)
      => JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters });

    volatile Peer? _runtime;
    readonly ConcurrentDictionary<string, Peer> _controllers = new();
    readonly ConcurrentDictionary<string, Peer> _pending = new();

    sealed class Peer(WebSocket socket)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public bool Open => socket.State == WebSocketState.Open;

        public async Task<string> Receive(CancellationToken cancel)
        {
            var buffer = new byte[8192];
            using var stream = new MemoryStream();

            while (true)
            {
                var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancel);
                if (received.MessageType == WebSocketMessageType.Close)
                    throw new WebSocketException("connection closed");

                stream.Write(buffer, 0, received.Count);
                if (stream.Length > 1024 * 1024)
                    throw new InvalidDataException("message is larger than 1 MB");

                if (received.EndOfMessage)
                    return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public async Task Send(string json, CancellationToken cancel)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _send.WaitAsync(cancel);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                                       true, cancel);
            }
            finally { _send.Release(); }
        }

        public async Task Close(WebSocketCloseStatus status, string description)
        {
            if (socket.State != WebSocketState.Open) return;

            try
            {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseAsync(status, description, cancel.Token);
            }
            catch (Exception error) when (error is WebSocketException ||
                                          error is OperationCanceledException ||
                                          error is ObjectDisposedException) { }
        }

        readonly SemaphoreSlim _send = new(1, 1);
    }
}
