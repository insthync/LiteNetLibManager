# LiteNetLibManager

Networking components for Unity built on LiteNetLib. `LiteNetLibManager` handles transports and messages; `LiteNetLibGameManager` adds scene, object, RPC, and state synchronization.

Start with the [documentation](docs/README.md), then see the [game manager setup](docs/basic/network_manager.md) and [state sync guide](docs/testing/state_sync_layout_results_2026-09-28.md). The published guide is also available at [insthync.github.io/LiteNetLibManager](https://insthync.github.io/LiteNetLibManager).

## Dependencies

- [LiteNetLib](https://github.com/RevenantX/LiteNetLib)
- [UniTask](https://github.com/Cysharp/UniTask)
- [ZLogger](https://github.com/Cysharp/ZLogger) and [ZString](https://github.com/Cysharp/ZString)
- [Fleck](https://github.com/Facepunch/Fleck) for WebSocket server support
- [Unity Addressable Asset Tools](https://github.com/insthync/unity-addressable-asset-tools) for addressable assets
