Jacquard Socket API
===================

This document describes the small localhost control surface added on the `socket-api`
branch. It is deliberately a socket API, not a command-line client, audio exporter or
second score engine. Jacquard remains responsible for parsing, editing, scheduling and
rendering the score.

Scope and topology
------------------

The API is a local JSON-RPC 2.0 protocol carried by a WebSocket connection:

```text
                         localhost only
  controller / observer  <----------------->  Socket relay
                                                  |
                                                  | one runtime connection
                                                  v
                                            Jacquard Unity app
                                            (RemoteBridge)
```

The default endpoint is `ws://127.0.0.1:38271/v1/ws`. The relay listens on loopback
only. `JACQUARD_SOCKET_PORT` changes the port for development and tests;
`JACQUARD_REMOTE_TOKEN` enables an optional shared token. The Unity runtime reads the
same token and may override its destination with `JACQUARD_REMOTE_URL`.

Components
----------

### Unity runtime bridge

`Assets/Jacquard/Remote/RemoteBridge.cs` is an optional component added by
`JacquardApp.Start` on editor and standalone builds. It has four responsibilities:

1. Connect/reconnect to the relay and send a `session.hello` message with role
   `runtime`.
2. Receive WebSocket frames on a background task and enqueue complete text messages.
3. Drain that queue from Unity's `Update`, where `Project`, `Sequencer`, `ScoreEditor`
   and `Store` are safe to access.
4. Serialize responses/events on the background send path, guarded by one send gate so
   WebSocket sends never overlap.

The bridge never parses a partial score or edits Unity state from the network task.
`project.replace` is parsed with the existing `ProjectFormat.Read`; adoption is still
performed by `Sequencer.SwitchTo`, so a playing project changes at the next master-loop
boundary and a stopped project changes immediately. `ScoreEditor.Changed` is the one
source used to increment the bridge revision and broadcast project events.

### Socket relay

`Tools/Jacquard.Socket/Program.cs` is a minimal ASP.NET Core host. It owns no score and
does not write files. `SessionHub` owns the connection registry:

| State | Invariant |
| --- | --- |
| `_runtime` | At most one authenticated runtime is connected. |
| `_controllers` | All authenticated controller and observer peers. |
| `_pending` | Request ID to controller mapping for requests forwarded to the runtime. |

Each peer has an independent receive loop and a semaphore-serialized send path.
`ConcurrentDictionary` protects the shared registries. A controller request is first
validated by the relay, added to `_pending`, and forwarded unchanged to the runtime.
Only a runtime response with the matching string ID is routed back. Runtime
notifications whose method starts with `event.` are broadcast to every controller and
observer.

Connection and handshake state
------------------------------

Every connection must begin with a request like:

```json
{
  "jsonrpc": "2.0",
  "id": "hello-1",
  "method": "session.hello",
  "params": {
    "protocolVersion": 1,
    "role": "controller",
    "token": "optional-shared-token"
  }
}
```

The protocol intentionally accepts string request IDs only. `role` is one of:

| Role | Cardinality | Permissions |
| --- | --- | --- |
| `runtime` | exactly one | Receives forwarded requests; publishes events. |
| `controller` | many | May issue all runtime API requests. |
| `observer` | many | May issue only `session.get` and `project.get`. |

The handshake response contains `protocolVersion`, a relay `connectionId`, and whether
a runtime is currently connected. A second runtime receives `runtime_exists`. A missing
or incorrect token receives `unauthenticated`; handshake failures do not enter the
connection registry.

API reference
-------------

All calls use JSON-RPC 2.0 with a non-empty string `id`:

```text
call(method: string, params: object) -> result: object | error: RpcError
```

### `session.hello`

```text
session.hello(HelloParams) -> HelloResult

HelloParams = {
  protocolVersion: 1,
  role: "runtime" | "controller" | "observer",
  token?: string,
  clientName?: string,
  clientVersion?: string
}

HelloResult = {
  protocolVersion: 1,
  connectionId: string,
  runtimeConnected: boolean
}
```

Authenticates the connection and assigns its role. The runtime role is limited to one
connected peer; controllers and observers may be multiple.

### `session.get`

```text
session.get({ sessionId?: string }) -> {
  revision: number,
  pendingRevision: number,
  projectName: string,
  playing: boolean,
  switchPending: boolean,
  masterPass: number,
  playingStep: number,
  formatVersion: number
}
```

Returns the current score and transport state. `pendingRevision` is zero when no score
switch is waiting for a loop boundary.

### `project.get`

```text
project.get({ sessionId?: string }) -> {
  revision: number,
  name: string,
  formatVersion: number,
  content: string
}
```

Returns the complete current `.jacquard` document. `content` is never a partial patch.

### `project.replace`

```text
project.replace({
  sessionId?: string,
  baseRevision: number,
  content: string,
  operationId?: string,
  apply?: "next_loop",
  persist?: boolean
}) -> {
  status: "queued" | "applied",
  revision: number,
  pendingRevision: number
}
```

Validates and adopts a complete score. While playing, `apply: "next_loop"` switches at
the next master-lane boundary. `baseRevision` prevents overwriting a newer edit;
`persist` saves after adoption and `operationId` is returned in the project-changed
event.

### `project.save`

```text
project.save({ sessionId?: string }) -> {
  status: string,
  revision: number
}
```

Persists the runtime's current project through Jacquard's normal store.

### `transport.play` and `transport.stop`

```text
transport.play({ sessionId?: string }) -> {
  status: "playing" | "stopped",
  revision: number
}

transport.stop({ sessionId?: string }) -> {
  status: "playing" | "stopped",
  revision: number
}
```

Both operations are idempotent. `play` starts only when stopped; `stop` stops only when
playing.

### Events

Events are JSON-RPC notifications without an `id`:

```text
event.project.changed({
  revision: number,
  operationId?: string,
  source: "controller" | "user"
})

event.transport.changed({
  revision: number,
  operationId?: string,
  source: "playing" | "stopped"
})

event.runtime.disconnected({})
```

`event.project.changed` confirms that a score edit became the active project. Runtime
events are broadcast to every connected controller and observer.

### Errors

```text
RpcError = {
  code: -32000,
  message: string,
  data: { kind: string }
}
```

The stable `data.kind` values are listed in the [Errors and limits](#errors-and-limits)
section below.

Request routing
---------------

The relay currently forwards these runtime methods without changing their JSON:

| Method | Runtime behavior |
| --- | --- |
| `session.get` | Returns revision, transport state and loop position. |
| `project.get` | Returns the complete current `.jacquard` text and revision. |
| `project.replace` | Validates a complete project and switches it at `next_loop`. |
| `project.save` | Persists the runtime's current project. |
| `transport.play` | Starts playback if stopped. |
| `transport.stop` | Stops playback if playing. |

The relay does not add partial patch semantics. `project.replace` therefore remains
optimistic-concurrency controlled by the runtime:

1. Read `session.get` or `project.get` and retain `revision`.
2. Send the complete candidate score as `content` with `baseRevision`.
3. Treat `queued` as accepted but not yet visible; wait for `event.project.changed`.
4. On `revision_conflict`, fetch the new project and reapply the intended edit.

The bridge's `persist` flag and `operationId` are carried through to the runtime; the
relay does not interpret them.

Errors and limits
-----------------

Errors use JSON-RPC's `error` object and a stable `error.data.kind` value:

| Kind | Meaning |
| --- | --- |
| `invalid_handshake` | First message is not a valid protocol-v1 hello. |
| `unauthenticated` | Shared token is missing or incorrect. |
| `invalid_role` | Role is not `runtime`, `controller` or `observer`. |
| `runtime_exists` | A runtime is already connected. |
| `invalid_json` | An established controller sent malformed JSON; the connection remains usable. |
| `invalid_request` | A controller message lacks a non-empty string `id` or `method`. |
| `read_only` | An observer attempted a mutating method. |
| `runtime_not_connected` | No runtime is available to service a request. |
| `duplicate_id` | The ID is already waiting for a runtime response. |
| `runtime_disconnected` | A pending request could not be completed. |
| `validation_failed` / `revision_conflict` / `switch_pending` | Runtime-side score operation failed. |

Messages are capped at 1 MiB, including fragmented WebSocket messages. An oversized
message is closed with WebSocket status `MessageTooBig` (1009). The relay binds only to
loopback and has no TLS or Internet-listener mode; it should not be exposed by changing
the bind address without adding an explicit authentication and deployment boundary.

Failure and lifecycle behavior
------------------------------

* If a controller disconnects, its pending entries are removed immediately; a later
  runtime response is ignored because there is no longer a recipient.
* If the runtime disconnects, every pending request receives `runtime_disconnected`
  and observers/controllers receive `event.runtime.disconnected`.
* If the Unity app starts before the relay, `RemoteBridge` retries every 1.5 seconds.
  If the relay starts before Unity, the runtime handshake makes it available without
  restarting the relay.
* Closing a peer is idempotent. All sends are serialized per peer, preventing concurrent
  `SendAsync` calls from corrupting frame order.

Testing strategy
----------------

`Tools/Jacquard.Socket.Tests` is a black-box integration suite. It starts the real
compiled relay on a dynamically allocated loopback port and uses .NET's
`ClientWebSocket`; no Unity license, scene, audio device or third-party WebSocket
implementation is needed.

Run it with:

```sh
dotnet test Tools/Jacquard.Socket.Tests/Jacquard.Socket.Tests.csproj
```

The suite verifies:

* HTTP-versus-WebSocket endpoint behavior;
* mandatory protocol-v1 handshake, role validation and token authentication;
* single-runtime enforcement and connection-state reporting;
* controller forwarding and response correlation;
* observer read-only enforcement and offline runtime errors;
* duplicate pending IDs without losing the first request;
* malformed JSON, non-object requests and non-2.0 JSON-RPC messages;
* event broadcast to both controller and observer peers;
* pending-request failure and cleanup on runtime disconnect; and
* the 1 MiB limit and graceful `MessageTooBig` close handshake.

The Unity-side bridge is additionally checked by the project's normal Unity batch-mode
script compilation and by the existing `Jacquard > Run Self Test` menu test. Those tests
remain the authority for score parsing, loop-boundary switching and audio semantics;
the socket suite deliberately tests only the transport boundary.

Non-goals
---------

The socket layer contains no CLI, SDK, audio export path, alternate score format,
remote file API or Internet service. A client remains a thin JSON-RPC client over this
endpoint; score and sequencer logic stay in Jacquard.
