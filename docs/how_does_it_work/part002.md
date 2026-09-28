# How it works, part 2: connection and ready flow

`LiteNetLibGameManager` extends `LiteNetLibManager` with requests, scene loading, player readiness, objects, RPCs, and state synchronization. The game request IDs (`GameReqTypes`) are carried inside the request/response message types (`GameMsgTypes.Request` and `Response`); they are not separate top-level packet IDs.

## Join sequence

1. The server accepts a transport connection, assigns a connection ID, and creates a `LiteNetLibPlayer`.
2. The client's `OnClientConnected()` sends `SendClientEnterGame()` unless `doNotEnterGameOnConnect` is true. The request contains the packet version and any bytes written by `SerializeEnterGameData`.
3. The server's `DeserializeEnterGameData` checks the request. The default implementation compares packet versions. On success, the enter-game response contains the connection ID and current `ServerSceneInfo`.
4. The client stores `ClientConnectionId` and loads or initializes the server scene. On later server scene changes, `GameMsgTypes.ServerSceneChange` carries the new scene information.
5. After the online scene and its network assets are registered, the client sends `SendClientReady()` unless `doNotReadyOnSceneLoaded` is true. The request starts with a state sync layout fingerprint. The server checks it before calling `SetPlayerReady`, marking the player ready, spawning the player prefab, or invoking `DeserializeClientReadyData`. A mismatch returns an error and no state is sent to that player.
6. The ready response also carries the server's fingerprint. The client refuses the connection if the header is absent or the layout differs; otherwise it passes any remaining response data to `ReadExtraClientReadyResponse`.
7. The interest manager subscribes the ready player to eligible objects. Spawn state is queued for delivery to that client.

```text
Client                         Server
  |---- transport connect ------>|
  |---- EnterGame request ------->| validate version/custom data
  |<--- EnterGame response -------| connection ID + scene info
  | load/initialize online scene  |
  |---- ClientReady + layout ----->| compare layout, then mark ready and spawn player
  |<--- ClientReady + layout ------| compare layout
  |<--- subscribed spawn state ---| object IDs + initial state
```

The built-in top-level message IDs include `Request = 0`, `Response = 1`, `RPC = 2`, `SyncBaseLine = 3`, `SyncDelta = 4`, and `ServerSceneChange = 6`. `GameReqTypes.EnterGame = 0` and `ClientReady = 1` identify requests within the request message. See [part 1](part001.md) when registering custom packet IDs.

The fingerprint covers the registered prefab and scene-object IDs, behaviour types and order, and sync element names, declared types, concrete types, and null/present state. Registry insertion order does not affect it. It is calculated at each ready exchange after `LiteNetLibAssets.Initialize()`; register network assets before calling `SendClientReady()` manually. Both peers need this ClientReady format. The earlier `packetVersion` check still applies at EnterGame and should be advanced for changes outside the checked layout, such as custom serializers or RPC contracts. If a project spawns identities from a custom registry, override `CalculateSyncSchemaFingerprint()` to include them.

## Custom join data

Override the matching writer and reader on your game manager:

- `SerializeEnterGameData(NetDataWriter writer)` on the client and `DeserializeEnterGameData(uint requestId, long connectionId, EnterGameRequestMessage request, NetDataReader reader)` on the server. The reader returns `UniTask<bool>`; if overriding it, preserve version validation as needed.
- `SerializeClientReadyData(NetDataWriter writer)` on the client and `DeserializeClientReadyData(uint requestId, long connectionId, NetDataReader reader, LiteNetLibIdentity playerIdentity)` on the server. The reader returns `UniTask<bool>`; `playerIdentity` comes from `SpawnPlayer` and may be null when no player prefab is configured.

The manager writes and checks the layout header before calling the custom ClientReady serializers, so custom data remains in the same order relative to those hooks. Keep the automatic enter-game and ready flags aligned with your UI flow. For objects and subscriptions after ready, continue to [part 3](part003.md).
