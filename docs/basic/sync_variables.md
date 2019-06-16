## Sync Field

`LiteNetLibSyncField` will automatic sync data from server to clients, it must be defined in class which inherit from `LiteNetLibBehaviour` like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [SerializeField]
    private LiteNetLibSyncField<int> hp = new LiteNetLibSyncField<int>();
    [SerializeField]
    private LiteNetLibSyncField<int> mp = new LiteNetLibSyncField<int>();
}
```

You also able to set configs when declare it like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [SerializeField]
    private LiteNetLibSyncField<int> hp = new LiteNetLibSyncField<int>() { 
        sendInterval = 0.1f,
        syncMode = LiteNetLibSyncField.SyncMode.ServerToClients,
    };
    [SerializeField]
    private LiteNetLibSyncField<int> mp = new LiteNetLibSyncField<int>() { 
        sendInterval = 0.1f,
        syncMode = LiteNetLibSyncField.SyncMode.ServerToClients,
    };
}
```

About configs there are:

- `sendOptions`, how it sync to clients. For some data such as character position may sync as `Sequenced` to send data in order but not have to confirm that all data that client will receive. For some data such as character health may sync as `ReliableOrdered` so send data in order and confirm that client will receives them.
- `sendInterval`, this is interval to sync data, data will not sync to client immediately when there are changes, it will send by this interval.
- `alwaysSync`, If this is **TRUE** it will syncing although no changes.
- `doNotSyncInitialDataImmediately`, If this is **TRUE** it will not sync initial data immdediately with spawn message (it will sync later)
- `syncMode`, how its changes handles, you have 3 choices for this. 1) `ServerToClients` Changes handle by server, will send to connected clients when changes occurs on server. 2) `ServerToOwnerClient` Changes handle by server, will send to owner-client when changes occurs on server. 3) `ClientMulticast` Changes handle by owner-client, will send to server then server multicast to other clients when changes occurs on owner-client.
- `onChange(data)`, event when data changes on clients.


Now it's supported with following types:

```
bool, bool[], byte, char, double, double[], float, float[], int, int[], long, long[], sbyte, short, short[], string, uint, uint[], ulong, ulong[], ushort, ushort[], Color, Quaternion, Vector2, Vector2Int, Vector3, Vector3Int, Vector4
```

But you can make it support other type by implement `INetSerializable` interface like this:

```
using LiteNetLib.Utils;
public struct CharacterStats : INetSerializable {
    public int atk;
    public int def;

    // Implement interface
    public void Deserialize(NetDataReader reader)
    {
        atk = reader.GetInt();
        def = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(atk);
        writer.Put(def);
    }
}
```

Then you can use it like this

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [SerializeField]
    private LiteNetLibSyncField<CharacterStats> hp = new LiteNetLibSyncField<CharacterStats>();
}
```

## Sync List

`LiteNetLibSyncList` will automatic sync list data from server to clients, it must be defined in class which inherit from `LiteNetLibBehaviour` like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [SerializeField]
    private LiteNetLibSyncList<int> itemIds = new LiteNetLibSyncList<int>();
}
```

You also able to set configs when declare it like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    [SerializeField]
    private LiteNetLibSyncList<int> itemIds = new LiteNetLibSyncList<int>() { 
        forOwnerOnly = false,
    };
}
```

About configs there are:

- `forOwnerOnly`, if this is **TRUE** it will send data to owner client only
- `onOperation(operationType, itemIndex)`, event when process operations on clients

*(Sync List have no `sendOptions`, `sendInterval` configs because it have to send update immediately and must be reached to clients in order so its `sendOptions` will forced to `ReliableOrdered`)*

Its supported types is like as `LiteNetLibSyncField` and also able to create custom types like it too.