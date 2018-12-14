# Net Function

`LiteNetLibNetFunction` is way to perform actions across the network. to declare / regoster `LiteNetLibNetFunction` you should do it in overrided `OnSetup()` function like this:

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
}
```

Then you can call it to invoke callback with parameters on target like this:

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

There are 3 types of net function calls:
- `CallNetFunction(Shoot, FunctionReceivers.Server, bulletType)`, call `Shoot` function from client to server
- `CallNetFunction(Shoot, FunctionReceivers.All, bulletType)`, call `Shoot` function from client or server to all clients, if it is call from client it will send data to server with parameters then server send received data to all clients
- `CallNetFunction(Shoot, targetConnectionId, bulletType)`, call `Shoot` function from client or server to target client, if it is call from client it will send data to server with parameters then server send received data to target clients