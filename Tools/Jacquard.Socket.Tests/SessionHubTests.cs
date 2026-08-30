using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Xunit;

namespace Jacquard.Socket.Tests;

[Collection(SocketCollection.Name)]
public sealed class SessionHubTests(SocketServerFixture server)
{
    [Fact]
    public async Task Http_endpoint_rejects_non_websocket_requests()
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(server.HttpUrl);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Handshake_requires_session_hello()
    {
        using var socket = await server.ConnectAsync();
        await socket.SendJsonAsync(new { jsonrpc = "2.0", id = "1", method = "session.get" });

        using var response = await socket.ReceiveJsonAsync();
        Assert.Equal("invalid_handshake", ErrorKind(response));
    }

    [Fact]
    public async Task Handshake_rejects_malformed_json()
    {
        using var socket = await server.ConnectAsync();
        await socket.SendTextAsync("{");

        using var response = await socket.ReceiveJsonAsync();
        Assert.Equal("invalid_handshake", ErrorKind(response));
    }

    [Fact]
    public async Task Handshake_requires_a_valid_role()
    {
        using var socket = await server.ConnectAsync();
        await socket.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "1", method = "session.hello",
            @params = new { protocolVersion = 1, role = "writer", token = server.Token }
        });

        using var response = await socket.ReceiveJsonAsync();
        Assert.Equal("invalid_role", ErrorKind(response));
    }

    [Fact]
    public async Task Handshake_rejects_unsupported_protocol_versions()
    {
        using var socket = await server.ConnectAsync();
        await socket.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "1", method = "session.hello",
            @params = new { protocolVersion = 2, role = "controller", token = server.Token }
        });

        using var response = await socket.ReceiveJsonAsync();
        Assert.Equal("invalid_handshake", ErrorKind(response));
    }

    [Fact]
    public async Task Configured_token_is_required()
    {
        using var socket = await server.ConnectAsync();
        using var response = await socket.HelloAsync("controller", "wrong-token");

        Assert.Equal("unauthenticated", ErrorKind(response));
    }

    [Fact]
    public async Task Runtime_and_controller_handshakes_report_connection_state()
    {
        using var controller = await server.ConnectAsync();
        using (var before = await controller.HelloAsync("controller", server.Token))
            Assert.False(before.RootElement.GetProperty("result").GetProperty("runtimeConnected").GetBoolean());

        using var runtime = await server.ConnectAsync();
        using (var runtimeHello = await runtime.HelloAsync("runtime", server.Token))
            Assert.True(runtimeHello.RootElement.GetProperty("result").GetProperty("runtimeConnected").GetBoolean());

        using var secondController = await server.ConnectAsync();
        using var after = await secondController.HelloAsync("observer", server.Token);
        Assert.True(after.RootElement.GetProperty("result").GetProperty("runtimeConnected").GetBoolean());
    }

    [Fact]
    public async Task Only_one_runtime_can_be_connected()
    {
        using var first = await server.ConnectAsync();
        using var firstHello = await first.HelloAsync("runtime", server.Token);
        Assert.True(firstHello.RootElement.TryGetProperty("result", out _));

        using var second = await server.ConnectAsync();
        using var secondHello = await second.HelloAsync("runtime", server.Token);
        Assert.Equal("runtime_exists", ErrorKind(secondHello));
    }

    [Fact]
    public async Task Controller_request_is_forwarded_and_response_is_routed_back()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var controller = await server.ConnectAsync();
        using var controllerHello = await controller.HelloAsync("controller", server.Token);

        var request = new
        {
            jsonrpc = "2.0", id = "request-1", method = "session.get",
            @params = new { sessionId = "local" }
        };
        await controller.SendJsonAsync(request);

        using var forwarded = await runtime.ReceiveJsonAsync();
        Assert.Equal("request-1", forwarded.RootElement.GetProperty("id").GetString());
        Assert.Equal("session.get", forwarded.RootElement.GetProperty("method").GetString());

        await runtime.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "request-1",
            result = new { revision = 4, playing = false }
        });

        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal(4, response.RootElement.GetProperty("result").GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task Observer_can_read_but_cannot_mutate()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var observer = await server.ConnectAsync();
        using var observerHello = await observer.HelloAsync("observer", server.Token);

        await observer.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "write", method = "transport.play",
            @params = new { }
        });
        using var denied = await observer.ReceiveJsonAsync();
        Assert.Equal("read_only", ErrorKind(denied));

        await observer.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "read", method = "project.get",
            @params = new { }
        });
        using var forwarded = await runtime.ReceiveJsonAsync();
        Assert.Equal("read", forwarded.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Controller_without_runtime_gets_runtime_not_connected()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        await controller.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "offline", method = "session.get",
            @params = new { }
        });
        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("runtime_not_connected", ErrorKind(response));
    }

    [Fact]
    public async Task Duplicate_pending_ids_are_rejected_without_losing_the_first_request()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var controller = await server.ConnectAsync();
        using var controllerHello = await controller.HelloAsync("controller", server.Token);

        var request = new { jsonrpc = "2.0", id = "same", method = "session.get", @params = new { } };
        await controller.SendJsonAsync(request);
        using var firstForwarded = await runtime.ReceiveJsonAsync();
        await controller.SendJsonAsync(request);

        using var duplicate = await controller.ReceiveJsonAsync();
        Assert.Equal("duplicate_id", ErrorKind(duplicate));

        await runtime.SendJsonAsync(new { jsonrpc = "2.0", id = "same", result = new { ok = true } });
        using var firstResponse = await controller.ReceiveJsonAsync();
        Assert.True(firstResponse.RootElement.GetProperty("result").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Invalid_controller_requests_receive_a_protocol_error()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        await controller.SendJsonAsync(new { jsonrpc = "2.0", method = "session.get" });
        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("invalid_request", ErrorKind(response));
    }

    [Fact]
    public async Task Requests_must_use_json_rpc_version_two()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        await controller.SendJsonAsync(new
        {
            jsonrpc = "1.0", id = "wrong-version", method = "session.get",
            @params = new { }
        });
        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("invalid_request", ErrorKind(response));
    }

    [Fact]
    public async Task Invalid_json_on_an_established_controller_connection_is_rejected_and_connection_survives()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        await controller.SendTextAsync("{");
        using var invalid = await controller.ReceiveJsonAsync();
        Assert.Equal("invalid_json", ErrorKind(invalid));

        await controller.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "still-alive", method = "session.get",
            @params = new { }
        });
        using var offline = await controller.ReceiveJsonAsync();
        Assert.Equal("runtime_not_connected", ErrorKind(offline));
    }

    [Fact]
    public async Task Non_object_controller_messages_are_rejected()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        await controller.SendTextAsync("[]");
        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("invalid_request", ErrorKind(response));
    }

    [Fact]
    public async Task Runtime_events_are_broadcast_to_controllers_and_observers()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var controller = await server.ConnectAsync();
        using var controllerHello = await controller.HelloAsync("controller", server.Token);
        using var observer = await server.ConnectAsync();
        using var observerHello = await observer.HelloAsync("observer", server.Token);

        await runtime.SendJsonAsync(new
        {
            jsonrpc = "2.0", method = "event.project.changed",
            @params = new { revision = 9, source = "controller" }
        });

        using var controllerEvent = await controller.ReceiveJsonAsync();
        using var observerEvent = await observer.ReceiveJsonAsync();
        Assert.Equal("event.project.changed", controllerEvent.RootElement.GetProperty("method").GetString());
        Assert.Equal("event.project.changed", observerEvent.RootElement.GetProperty("method").GetString());
    }

    [Fact]
    public async Task Pending_requests_fail_when_runtime_disconnects()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var controller = await server.ConnectAsync();
        using var controllerHello = await controller.HelloAsync("controller", server.Token);

        await controller.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "pending", method = "session.get",
            @params = new { }
        });
        using var forwarded = await runtime.ReceiveJsonAsync();
        await runtime.CloseAsync();

        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("runtime_disconnected", ErrorKind(response));
        Assert.Equal("pending", response.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Controller_disconnect_removes_its_pending_id()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var firstController = await server.ConnectAsync();
        using var firstHello = await firstController.HelloAsync("controller", server.Token);

        await firstController.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "reusable", method = "session.get",
            @params = new { }
        });
        using var firstForwarded = await runtime.ReceiveJsonAsync();
        await firstController.CloseAsync();

        using var secondController = await server.ConnectAsync();
        using var secondHello = await secondController.HelloAsync("controller", server.Token);
        await secondController.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "reusable", method = "session.get",
            @params = new { }
        });

        using var secondForwarded = await runtime.ReceiveJsonAsync();
        Assert.Equal("reusable", secondForwarded.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Invalid_runtime_json_closes_only_the_runtime_connection()
    {
        using var runtime = await server.ConnectAsync();
        using var runtimeHello = await runtime.HelloAsync("runtime", server.Token);
        using var controller = await server.ConnectAsync();
        using var controllerHello = await controller.HelloAsync("controller", server.Token);

        await runtime.SendTextAsync("{");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var close = await runtime.ReceiveAsync(new ArraySegment<byte>(new byte[128]), cancel.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);

        await controller.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "offline", method = "session.get",
            @params = new { }
        });
        using var response = await controller.ReceiveJsonAsync();
        Assert.Equal("runtime_not_connected", ErrorKind(response));
    }

    [Fact]
    public async Task Messages_over_one_megabyte_close_the_connection()
    {
        using var controller = await server.ConnectAsync();
        using var hello = await controller.HelloAsync("controller", server.Token);

        var oversized = new string('x', 1024 * 1024 + 1);
        await controller.SendJsonAsync(new
        {
            jsonrpc = "2.0", id = "large", method = "project.replace",
            @params = new { content = oversized }
        });

        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await controller.ReceiveAsync(new ArraySegment<byte>(new byte[128]), cancel.Token);
        Assert.Equal(WebSocketMessageType.Close, received.MessageType);
    }

    static string ErrorKind(JsonDocument document)
      => document.RootElement.GetProperty("error").GetProperty("data").GetProperty("kind").GetString()!;
}
