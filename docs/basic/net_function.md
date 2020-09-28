# Net Function (RPC)

`RPC` is way to perform actions across the network. 

There are 4 types of RPCs:
- ServerRpc, it will be called from client to do something at server.
- AllRpc, it will be called from server to do something at server and all clients.
- TargetRpc, it will be called from server or client to do something at target client by connection ID.
- ElasticRpc/NetFunction, it can be any RPC up to how you call set receivers type when call it with `RPC()` or `CallNetFunction()` functions.

## Declaring RPC functions

### Declaring RPC functions by attributes

To declare `RPC` functions you can use attributes (`[ServerRpc]`, `[AllRpc]`, `[TargetRpc]`, `[ElasticRpc]`, `[NetFunction]`).

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [ElasticRpc]
    private void Shoot(int bulletType)
    {
        // Received `bulletType` to do anything
    }
}
```

### Declaring RPC functions by functions

Or use register functions (`RegisterElasticRPC`, `RegisterElasticRPC`, `RegisterElasticRPC`, `RegisterElasticRPC`, `RegisterNetFunction`) which you should do it in overrided `OnSetup()` function like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    public override void OnSetup() {
        base.OnSetup();
        RegisterElasticRPC<int>(Shoot);
    }

    private void Shoot(int bulletType)
    {
        // Received `bulletType` to do anything
    }
}
```

## Calling RPC functions

Then you can call `RPC` by `RPC()` or `CallNetFunction()` functions it to invoke callback with parameters on target like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    public override void OnSetup() {
        base.OnSetup();
        RegisterNetFunction<int>(Shoot);
    }

    private void Shoot(int bulletType)
    {
        // Received `bulletType` to do anything
    }

    public void DoShoot(int bulletType)
    {
        // Call Shoot at server
        CallNetFunction(Shoot, FunctionReceivers.Server, bulletType);
    }
}
```

To call `ServerRpc` or `ClientRpc`, you can use `RPC()` function by set function which you want to call to first parameter following with parameters values to later parameters like this:

```
public void CallShootAll(int bulletType)
{
    RPC(Shoot, bulletType);
}
```

To call `TargetRpc` it's similar but you have to set target connection ID to second paramter like this:

```
public void CallShootAtOwnerClient(int bulletType)
{
    RPC(Shoot, ConnectionId, bulletType);
}
```

For elastic RPCs if you use `RPC()` function it will call all RPC by default, if you want to change receivers target you have to set receivers target to second parameter like this:

```
public void CallShootAll(int bulletType)
{
    RPC(Shoot, FunctionReceivers.All, bulletType);
}

public void CallShootAtServer(int bulletType)
{
    RPC(Shoot, FunctionReceivers.Server, bulletType);
}
```

**A word `NetFunction` had the same meaning with `ElasticRPC` and do the same thing, so: `[NetFunction]` == `[ElasticRpc]`, `RegisterNetFunction()` == `RegisterElasticRPC()` and `CallNetFunction()` == `RPC()`**