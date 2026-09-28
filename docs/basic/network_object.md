# Identity and behaviour

## Identity

Put one `LiteNetLibIdentity` on each networked scene object or prefab. The server assigns its runtime `ObjectId`; clients use that ID to find later updates. Register runtime prefabs in `LiteNetLibAssets.spawnablePrefabs` on both server and client. Scene objects need matching scene-object IDs across builds.

`LiteNetLibIdentity` finds `LiteNetLibBehaviour` components on itself and its active children with `GetComponentsInChildren<LiteNetLibBehaviour>()`. Keep their types, discovery order, and sync field names consistent between builds. At ClientReady, the manager compares registered prefab and scene-object layouts and refuses a mismatch before sending state. The [layout tests](../testing/state_sync_layout_results_2026-09-28.md) explain the packet reader and readiness results.

## Behaviour

Derive a component from `LiteNetLibBehaviour` to add sync fields, sync lists, and RPCs. Useful properties include `IsServer`, `IsClient`, and `IsOwnerClient`. Ownership is based on the identity's owner connection ID; an owned object need not be the player character.

Current overridable lifecycle hooks include:

- `OnIdentityInitialize()` and `OnIdentityDestroy()` when the identity is initialized or destroyed.
- `OnSetup()` to configure callbacks or register RPCs before sync elements are set up.
- `OnStartServer()`, `OnStartClient()`, and `OnStartOwnerClient()` when the object starts on each side.
- `OnSetOwnerClient(bool isOwnerClient)` when client ownership is applied or removed.
- `InitTransform(Vector3 position, Quaternion rotation)` for initial spawn transform handling.
- `OnNetworkDestroy(byte reasons)` for a network destroy.
- `OnServerSubscribingAdded()` and `OnServerSubscribingRemoved()` for server subscription changes.

The server's `BaseInterestManager` decides which players subscribe to an object. The included `DefaultInterestManager` uses visibility and distance checks. Attach a custom interest manager to the manager GameObject, or assign `LiteNetLibGameManager.InterestManager`, to change subscription policy. See [object lifecycle](../how_does_it_work/part003.md).
