# RPCs

An RPC calls a registered `LiteNetLibBehaviour` method across the connection. Declare the method with an attribute or register a delegate in `OnSetup()`. Keep method names, parameter types, and behaviour layouts compatible between server and clients.

| Attribute | Destination |
| --- | --- |
| `[ServerRpc]` | Server |
| `[AllRpc]` | Server and eligible subscribed clients |
| `[TargetRpc]` | One eligible target client |
| `[ElasticRpc]` | Chosen at the call site with `RPCReceivers` |

`[NetFunction]` is an alias for `[ElasticRpc]`. For manual registration use `RegisterServerRPC`, `RegisterAllRPC`, `RegisterTargetRPC`, or `RegisterElasticRPC`. `RegisterNetFunction` is an alias for elastic registration.

```csharp
using LiteNetLibManager;

public sealed class CombatMessages : LiteNetLibBehaviour
{
    [ServerRpc]
    private void RequestShot(int bulletType)
    {
        // Validate and process the request on the server.
    }

    [AllRpc]
    private void ShowShot(int bulletType)
    {
        // Show a server-approved effect.
    }

    [TargetRpc]
    private void ShowNotice(int noticeId)
    {
        // Show an effect on one target client.
    }

    [ElasticRpc]
    private void SendFlexible(int value)
    {
        // Called at the destination selected below.
    }

    public void Shoot(int bulletType)
    {
        if (IsOwnerClient)
            RPC(RequestShot, bulletType);
    }

    public void BroadcastShot(int bulletType)
    {
        if (IsServer)
            RPC(ShowShot, bulletType);
    }

    public void Notify(long connectionId, int noticeId)
    {
        if (IsServer)
            RPC(ShowNotice, connectionId, noticeId);
    }

    public void SendToServer(int value)
    {
        RPC(SendFlexible, RPCReceivers.Server, value);
    }
}
```

The no-receiver overload chooses the destination registered for `ServerRpc` or `AllRpc`. For `TargetRpc`, pass the target connection ID. For an elastic RPC, pass `RPCReceivers.Server`, `All`, or `Target` as appropriate; a target also needs a connection ID. `RPC` overloads can specify a data channel and `DeliveryMethod`.

The RPC send path checks caller ownership or server authority, and recipients must be eligible for the object. Validate client-supplied values inside server RPCs before changing authoritative game state. See [identity and behaviour](network_object.md) for ownership and the [object lifecycle](../how_does_it_work/part003.md) for subscriptions.
