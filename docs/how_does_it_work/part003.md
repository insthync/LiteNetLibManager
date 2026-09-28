# How it works, part 3: object lifecycle

A network object has a `LiteNetLibIdentity` and optional `LiteNetLibBehaviour` components. It can be a scene object with a matching scene ID on each peer or a runtime prefab registered in `LiteNetLibAssets.spawnablePrefabs` on both peers. The server creates and removes authoritative objects; clients resolve incoming scene or asset IDs and apply state.

## Spawn and subscription

Call `LiteNetLibAssets.NetworkSpawn(...)` on the server for a runtime object. The identity receives a runtime object ID and owner connection ID. The server's `BaseInterestManager` decides which ready players should subscribe. The included `DefaultInterestManager` updates subscriptions by visibility and distance; it also keeps an owned player's objects subscribed.

A new subscription queues `GameStateSyncType.Spawn` for that player. In the next reliable ordered `GameMsgTypes.SyncBaseLine` packet, the spawn contains the scene or asset ID, transform, object ID, owner ID, and initial sync elements allowed for that player. The client resolves or activates the matching object, applies its initial state, and invokes its lifecycle callbacks. `ServerToOwnerClient` fields are included only for the subscribed owner.

```text
Server spawns object -> interest manager adds subscription
  -> per-player Spawn state -> reliable SyncBaseLine
  -> client resolves identity and reads allowed initial elements
```

Scene objects use the same spawn state type as runtime prefabs; the record says whether it names a scene object or an asset. The [state sync guide](../testing/state_sync_layout_results_2026-09-28.md) explains element IDs and compatibility when layouts differ.

## Updates and removal

The server queues changed fields and list operations for subscribed players. Field values can travel in `SyncDelta` or `SyncBaseLine`; list operations use the reliable baseline. When an object is destroyed or a player unsubscribes, the server queues `GameStateSyncType.Destroy` in the baseline with an object ID and reason. `DestroyObjectReasons.RequestedToDestroy` means the object was removed; `RemovedFromSubscribing` means that player should stop seeing it. The client removes or deactivates its matching object.

```text
Field/list change -> eligible subscribed players -> Data state
Destroy/unsubscribe -> affected players -> Destroy state
Client receives -> find object by ID -> apply change or remove it
```

Use `LiteNetLibAssets.NetworkDestroy(..., byte reasons)` for an explicit server removal. To customize who sees an object, implement a `BaseInterestManager` or adjust identity visibility settings; old behaviour-level subscription hooks no longer exist. See [identity and behaviour](../basic/network_object.md) for current hooks.
