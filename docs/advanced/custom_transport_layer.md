# Transport layers

`LiteNetLibManager` builds an `ITransport` through a `BaseTransportFactory`. By default it uses `LiteNetLibTransportFactory`. Add another factory component and assign it to the manager's `TransportFactory` to select it.

The included choices are:

| Factory | Client and server behavior |
| --- | --- |
| `LiteNetLibTransportFactory` | LiteNetLib transport; configure its `connectKey` for connection validation |
| `WebSocketTransportFactory` | WebSocket transport, with optional secure mode and certificate settings |
| `MixTransportFactory` | LiteNetLib and WebSocket listeners on different server ports |

If `useWebSocket` is enabled without a compatible factory, the manager creates a `WebSocketTransportFactory`. WebGL clients require a WebSocket compatible factory; the manager selects one when needed.

## Mix transport

`MixTransportFactory` starts LiteNetLib at `networkPort` and WebSocket at `networkPort + webSocketPortOffset`. Its default offset is 100. A mix client uses LiteNetLib unless `ShouldUseWebSocket` is true or the platform is WebGL. Set the same base port and offset on server and client. The mix server allocates half of `maxConnections` to each listener, so choose a capacity that fits both.

For a different transport, implement `ITransport` and create a `BaseTransportFactory` whose `Build()` returns your implementation. Assign the factory before the manager initializes. Match client and server packet framing, channels, and delivery behavior. See [part 1](../how_does_it_work/part001.md) for message routing.
