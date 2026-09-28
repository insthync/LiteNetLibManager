# Game manager

Add `LiteNetLibGameManager` to a GameObject in the starting scene. Its required `LiteNetLibAssets` component stores the scene and prefab references. Register the same network prefabs on server and clients so incoming spawns resolve to the same asset.

## Configure a session

On the manager, set `networkAddress` and `networkPort` for the connection, `maxConnections` for server capacity, and `updateFps` for the logic update rate. Set `TransportFactory` to a `BaseTransportFactory` component if you need a specific transport. `useWebSocket` chooses the included WebSocket transport when no compatible custom factory is selected. The LiteNetLib transport's connection key belongs to its factory, not to the manager. See [transports](../advanced/custom_transport_layer.md).

Game settings include `packetVersion` (checked by the default enter-game handler), `pingDuration`, and `baseLineSyncInterval`. After scene loading, ClientReady also checks that registered network object layouts match before the server marks the player ready. The following switches change the automatic flow:

- `doNotEnterGameOnConnect`: call `SendClientEnterGame()` yourself after connecting.
- `doNotReadyOnSceneLoaded`: call `SendClientReady()` yourself after loading the online scene.
- `doNotDestroyOnSceneChanges`: keep the manager GameObject when returning to the offline scene; it already survives online scene loading.
- `loadOfflineSceneWhenClientStopped`: control whether stopping a client loads the offline scene.

On `LiteNetLibAssets`, set `offlineScene` and `onlineScene` (or their addressable equivalents). Set `playerPrefab` for automatic player spawning and add runtime network prefabs to `spawnablePrefabs`. `playerSpawnRandomly` selects random rather than ordered spawn points.

## Start and stop

Call `StartServer()` for a dedicated server, `StartClient()` or `StartClient(address, port)` for a client, and `StartHost()` for a server and client in one process. These start methods return `bool`. Call `StopServer()`, `StopClient()`, or `StopHost()` to stop them.

Override manager callbacks as needed: `OnPeerConnected(long connectionId)`, `OnPeerDisconnected(long connectionId, DisconnectReason reason, SocketError socketError)`, `OnClientConnected()`, `OnClientDisconnected(DisconnectReason reason, SocketError socketError, byte[] data)`, `OnStartServer()`, `OnStartClient(LiteNetLibClient client)`, and their matching stop callbacks. The manager also exposes server and client network error callbacks. Preserve base behavior when overriding a callback that has game-manager logic.

## Scenes and players

The enter-game response carries the server scene. After loading it, the client sends a ready request unless automatic ready is disabled. The server marks the player ready and calls `SpawnPlayer`, which uses the configured player prefab when available. The [connection workflow](../how_does_it_work/part002.md) describes the sequence.

To send custom ready data, override `SerializeClientReadyData(NetDataWriter writer)` on the client and `DeserializeClientReadyData(uint requestId, long connectionId, NetDataReader reader, LiteNetLibIdentity playerIdentity)` on the server. The latter returns `UniTask<bool>`. The manager's layout header is handled before these hooks. For data sent before scene loading, use `SerializeEnterGameData` and `DeserializeEnterGameData` instead. Keep the client and server serializers in the same order, register all network assets before a manual `SendClientReady()`, and keep `packetVersion` current for protocol changes outside the layout check.

Call `ServerSceneChange(ServerSceneInfo serverSceneInfo)` on the server to load a new scene and notify clients. `LiteNetLibAssets` exposes scene load start, progress, finish, and failure events. To create a registered object during play, use `LiteNetLibAssets.NetworkSpawn(...)` on the server; use `NetworkDestroy(..., byte reasons)` to remove it. See the [object lifecycle](../how_does_it_work/part003.md) and [state sync guide](../testing/state_sync_layout_results_2026-09-28.md).
