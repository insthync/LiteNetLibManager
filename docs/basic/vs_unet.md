# Legacy UNET mapping

This page maps older UNET terms to the current LiteNetLibManager API. It is a migration aid, not a protocol compatibility layer.

| Older UNET concept | LiteNetLibManager equivalent |
| --- | --- |
| `NetworkBehaviour` | `LiteNetLibBehaviour` on a `LiteNetLibIdentity` |
| `[SyncVar]` | A `LiteNetLibSyncField<T>` member, usually a concrete type such as `SyncFieldInt` |
| `SyncList<T>` | `LiteNetLibSyncList<T>`, usually a concrete type such as `SyncListInt` |
| `[Command]` | `[ServerRpc]` |
| `[ClientRpc]` | `[AllRpc]` |
| `[TargetRpc]` | `[TargetRpc]` |
| `isServer` / `isClient` / `isLocalPlayer` | `IsServer` / `IsClient` / `IsOwnerClient` |

For example, a networked integer field is declared on a behaviour like this:

```csharp
using LiteNetLibManager;
using UnityEngine;

public sealed class HealthState : LiteNetLibBehaviour
{
    [SerializeField] private SyncFieldInt health = new SyncFieldInt();

    public int Health => health.Value;

    public void SetHealth(int value)
    {
        if (IsServer)
            health.Value = value;
    }
}
```

There is no `[SyncField]` attribute for an ordinary `int` in this library. RPC methods use `[ServerRpc]`, `[AllRpc]`, or `[TargetRpc]` and are invoked through `RPC(...)`. See [sync fields and lists](sync_variables.md) and [RPCs](net_function.md) for current examples. Ownership and subscription behavior may differ from a legacy UNET project, so verify migration behavior in your game.
