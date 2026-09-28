# First networked object

This small exercise verifies the manager, prefab registration, and initial state flow before adding gameplay. It uses a player prefab; the same pattern applies to a projectile or other spawned object.

1. Add `LiteNetLibGameManager` to the starting scene and configure its `LiteNetLibAssets` component with an online scene.
2. Create a player prefab with `LiteNetLibIdentity` and the behaviour below. Assign it to `LiteNetLibAssets.playerPrefab` in both server and client builds.
3. Add the online scene to both builds. Start one instance with `StartHost()` and another with `StartClient(address, port)`.
4. After the client enters the scene and sends its ready request, the server spawns its player. The client should receive the player's initial health value.

```csharp
using LiteNetLibManager;
using UnityEngine;

public sealed class ExamplePlayerState : LiteNetLibBehaviour
{
    [SerializeField] private SyncFieldInt health = new SyncFieldInt();

    public int Health => health.Value;

    public override void OnSetup()
    {
        health.onChange += OnHealthChanged;
    }

    public override void OnStartServer()
    {
        health.Value = 100;
    }

    private void OnHealthChanged(bool initial, int oldValue, int newValue)
    {
        Debug.Log("Health: " + newValue);
    }
}
```

Server code can later change `health.Value` through a method that checks `IsServer`. Use a `[ServerRpc]` to request an action from the owning client, validate it on the server, and spawn a registered projectile through `Manager.Assets.NetworkSpawn(...)`. The server's interest manager decides which players receive that projectile's spawn and state. Continue with [RPCs](../basic/net_function.md), [object lifecycle](../how_does_it_work/part003.md), and the [state sync guide](../testing/state_sync_layout_results_2026-09-28.md).
