# Overview

The library separates transport from game state:

- `LiteNetLibManager` starts a server, client, or host and routes registered messages.
- `LiteNetLibGameManager` handles enter-game and ready requests, scene changes, spawned objects, RPCs, and state updates.
- `LiteNetLibAssets` stores scenes, the player prefab, and spawnable prefabs.
- `LiteNetLibIdentity` identifies a scene object or registered prefab across peers.
- `LiteNetLibBehaviour` provides object callbacks, RPC registration, sync fields, and sync lists.
- `BaseInterestManager` determines which players subscribe to each object.

The server assigns network object IDs and decides which subscribers receive their state. A host runs both server and client in one process. For setup and examples, continue with the [game manager](network_manager.md), [identity and behaviour](network_object.md), and [state sync guide](../testing/state_sync_layout_results_2026-09-28.md).
