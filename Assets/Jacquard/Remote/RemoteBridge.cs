using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Jacquard.App {

#if UNITY_EDITOR || UNITY_STANDALONE

// Localhost JSON-RPC bridge. Networking stays off the Unity thread; requests are
// applied from Update so project and transport state remain on the main thread.
[DefaultExecutionOrder(-100)]
sealed class RemoteBridge : MonoBehaviour
{
    public void Initialize(JacquardApp app)
    {
        _app = app;
        _app.Editor.Changed += OnProjectChanged;
        _ = ConnectLoop(_cancel.Token);
    }

    void Update()
    {
        while (_incoming.TryDequeue(out var json)) Handle(json);
    }

    void OnDestroy()
    {
        if (_app?.Editor != null) _app.Editor.Changed -= OnProjectChanged;
        _cancel.Cancel();
        _socket?.Abort();
        _socket?.Dispose();
    }

    async Task ConnectLoop(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var socket = new ClientWebSocket();
                _socket = socket;
                await socket.ConnectAsync(new Uri(Url), cancel);
                await Send(Hello(), cancel);
                _warned = false;
                await ReceiveLoop(socket, cancel);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (!cancel.IsCancellationRequested && !_warned)
                {
                    Debug.LogWarning("Jacquard socket bridge: " + error.Message);
                    _warned = true;
                }
            }
            finally
            {
                _socket?.Dispose();
                _socket = null;
            }

            if (!cancel.IsCancellationRequested)
                try { await Task.Delay(1500, cancel); }
                catch (OperationCanceledException) { }
        }
    }

    async Task ReceiveLoop(ClientWebSocket socket, CancellationToken cancel)
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult received;

            do
            {
                received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancel);
                if (received.MessageType == WebSocketMessageType.Close) return;
                stream.Write(buffer, 0, received.Count);
                if (stream.Length > MaxMessageBytes)
                    throw new InvalidDataException("message is larger than 1 MB");
            }
            while (!received.EndOfMessage);

            if (received.MessageType == WebSocketMessageType.Text)
                _incoming.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    void Handle(string json)
    {
        RpcRequest request;
        try { request = JsonUtility.FromJson<RpcRequest>(json); }
        catch (Exception error)
        {
            Debug.LogWarning("Jacquard socket bridge: " + error.Message);
            return;
        }

        if (request == null || string.IsNullOrEmpty(request.method)) return;

        try
        {
            switch (request.method)
            {
                case "session.get": SendSession(request.id); break;
                case "project.get": SendProject(request.id); break;
                case "project.replace": Replace(request); break;
                case "project.save": Save(request.id); break;
                case "transport.play": SetPlaying(request.id, true); break;
                case "transport.stop": SetPlaying(request.id, false); break;
                default: SendError(request.id, "method_not_found", request.method); break;
            }
        }
        catch (Exception error)
        {
            SendError(request.id, "operation_failed", error.Message);
        }
    }

    void SendSession(string id)
    {
        var master = _app.Sequencer.MasterRunner;
        Send(JsonUtility.ToJson(new SessionResponse
        {
            id = id,
            result = new SessionResult
            {
                revision = _revision,
                pendingRevision = _pendingProject == null ? 0 : _revision + 1,
                projectName = _app.Store.Name,
                playing = _app.Sequencer.IsPlaying,
                switchPending = _app.Sequencer.IsSwitchPending,
                masterPass = master?.Pass ?? 0,
                playingStep = master?.PlayingStep ?? -1,
                formatVersion = ProjectFormat.Version
            }
        }));
    }

    void SendProject(string id)
    {
        Send(JsonUtility.ToJson(new ProjectResponse
        {
            id = id,
            result = new ProjectResult
            {
                revision = _revision,
                name = _app.Store.Name,
                formatVersion = ProjectFormat.Version,
                content = ProjectFormat.Write(_app.Project)
            }
        }));
    }

    void Replace(RpcRequest request)
    {
        var args = request.@params ?? new RpcParams();

        if (args.baseRevision != _revision)
        {
            SendError(request.id, "revision_conflict",
                      "expected " + args.baseRevision + ", current " + _revision);
            return;
        }

        if (_app.Editor.Locked || _app.Sequencer.IsSwitchPending)
        {
            SendError(request.id, "switch_pending", "a project switch is already pending");
            return;
        }

        if (!string.IsNullOrEmpty(args.apply) && args.apply != "next_loop")
        {
            SendError(request.id, "unsupported_apply", "only next_loop is supported");
            return;
        }

        Project incoming;
        try { incoming = ProjectFormat.Read(args.content ?? ""); }
        catch (Exception error)
        {
            SendError(request.id, "validation_failed", error.Message);
            return;
        }

        _pendingProject = incoming;
        _pendingOperation = args.operationId;
        _saveAfterAdopt = args.persist;
        _app.Editor.Locked = true;
        _app.Sequencer.SwitchTo(incoming);

        var queued = _app.Sequencer.IsSwitchPending;
        Send(JsonUtility.ToJson(new CommandResponse
        {
            id = request.id,
            result = new CommandResult
            {
                status = queued ? "queued" : "applied",
                revision = _revision,
                pendingRevision = queued ? _revision + 1 : 0
            }
        }));
    }

    void Save(string id)
    {
        _app.Save();
        Send(JsonUtility.ToJson(new CommandResponse
        {
            id = id,
            result = new CommandResult { status = _app.Message, revision = _revision }
        }));
    }

    void SetPlaying(string id, bool playing)
    {
        if (_app.Sequencer.IsPlaying != playing) _app.TogglePlay();
        var status = _app.Sequencer.IsPlaying ? "playing" : "stopped";

        Send(JsonUtility.ToJson(new CommandResponse
        {
            id = id,
            result = new CommandResult { status = status, revision = _revision }
        }));
        SendEvent("event.transport.changed", null, status);
    }

    void OnProjectChanged()
    {
        var remote = _pendingProject != null && ReferenceEquals(_app.Project, _pendingProject);
        _revision++;

        if (remote)
        {
            if (_saveAfterAdopt) _app.Save();
            _app.Editor.Locked = false;
        }

        SendEvent("event.project.changed", remote ? _pendingOperation : null,
                  remote ? "controller" : "user");

        if (!remote) return;
        _pendingProject = null;
        _pendingOperation = null;
        _saveAfterAdopt = false;
    }

    void SendEvent(string method, string operationId, string source)
    {
        Send(JsonUtility.ToJson(new EventNotification
        {
            method = method,
            @params = new EventParams
            {
                revision = _revision,
                operationId = operationId,
                source = source
            }
        }));
    }

    string Hello() => JsonUtility.ToJson(new HelloRequest
    {
        id = "runtime-hello",
        @params = new HelloParams
        {
            protocolVersion = 1,
            role = "runtime",
            token = Environment.GetEnvironmentVariable("JACQUARD_REMOTE_TOKEN") ?? "",
            clientName = Application.productName,
            clientVersion = Application.version
        }
    });

    void SendError(string id, string kind, string message)
      => Send(JsonUtility.ToJson(new ErrorResponse
      {
          id = id,
          error = new RpcError
          {
              code = -32000,
              message = message,
              data = new ErrorData { kind = kind }
          }
      }));

    void Send(string json) => _ = Send(json, _cancel.Token);

    async Task Send(string json, CancellationToken cancel)
    {
        var socket = _socket;
        if (socket == null || socket.State != WebSocketState.Open) return;

        var bytes = Encoding.UTF8.GetBytes(json);
        var entered = false;
        try
        {
            await _sendGate.WaitAsync(cancel);
            entered = true;
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(new ArraySegment<byte>(bytes),
                                       WebSocketMessageType.Text, true, cancel);
        }
        catch (Exception error) when (error is WebSocketException ||
                                      error is OperationCanceledException ||
                                      error is ObjectDisposedException) { }
        finally { if (entered) _sendGate.Release(); }
    }

    string Url => Environment.GetEnvironmentVariable("JACQUARD_REMOTE_URL") ??
                  "ws://127.0.0.1:38271/v1/ws";

    const int MaxMessageBytes = 1024 * 1024;

    JacquardApp _app;
    ClientWebSocket _socket;
    readonly CancellationTokenSource _cancel = new();
    readonly SemaphoreSlim _sendGate = new(1, 1);
    readonly ConcurrentQueue<string> _incoming = new();

    long _revision = 1;
    Project _pendingProject;
    string _pendingOperation;
    bool _saveAfterAdopt;
    bool _warned;

    [Serializable] sealed class RpcRequest
    {
        public string id;
        public string method;
        public RpcParams @params;
    }

    [Serializable] sealed class RpcParams
    {
        public string operationId;
        public long baseRevision;
        public string apply;
        public bool persist;
        public string content;
    }

    [Serializable] sealed class HelloRequest
    {
        public string jsonrpc = "2.0";
        public string id;
        public string method = "session.hello";
        public HelloParams @params;
    }

    [Serializable] sealed class HelloParams
    {
        public int protocolVersion;
        public string role;
        public string token;
        public string clientName;
        public string clientVersion;
    }

    [Serializable] sealed class SessionResponse
    {
        public string jsonrpc = "2.0";
        public string id;
        public SessionResult result;
    }

    [Serializable] sealed class SessionResult
    {
        public long revision;
        public long pendingRevision;
        public string projectName;
        public bool playing;
        public bool switchPending;
        public int masterPass;
        public int playingStep;
        public int formatVersion;
    }

    [Serializable] sealed class ProjectResponse
    {
        public string jsonrpc = "2.0";
        public string id;
        public ProjectResult result;
    }

    [Serializable] sealed class ProjectResult
    {
        public long revision;
        public string name;
        public int formatVersion;
        public string content;
    }

    [Serializable] sealed class CommandResponse
    {
        public string jsonrpc = "2.0";
        public string id;
        public CommandResult result;
    }

    [Serializable] sealed class CommandResult
    {
        public string status;
        public long revision;
        public long pendingRevision;
    }

    [Serializable] sealed class ErrorResponse
    {
        public string jsonrpc = "2.0";
        public string id;
        public RpcError error;
    }

    [Serializable] sealed class RpcError
    {
        public int code;
        public string message;
        public ErrorData data;
    }

    [Serializable] sealed class ErrorData { public string kind; }

    [Serializable] sealed class EventNotification
    {
        public string jsonrpc = "2.0";
        public string method;
        public EventParams @params;
    }

    [Serializable] sealed class EventParams
    {
        public long revision;
        public string operationId;
        public string source;
    }
}

#endif

} // namespace Jacquard.App
