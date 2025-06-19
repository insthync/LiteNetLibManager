## Sync Field

`LiteNetLibSyncField` will automatic sync data from server to clients, it must be defined in class which inherit from `LiteNetLibBehaviour` like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    private LiteNetLibSyncField<int> hp = new LiteNetLibSyncField<int>();
    private LiteNetLibSyncField<int> mp = new LiteNetLibSyncField<int>();
}
```

You also able to set configs when declare it like this:

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    private LiteNetLibSyncField<int> hp = new LiteNetLibSyncField<int>() {
        syncMode = LiteNetLibSyncFieldMode.ServerToClients,
    };
    private LiteNetLibSyncField<int> mp = new LiteNetLibSyncField<int>() {
        syncMode = LiteNetLibSyncFieldMode.ServerToClients,
    };
}
```

About configs there are:

- `syncMode`, how its changes handles, you have 3 choices for this. 1) `ServerToClients` Changes handle by server, will send to connected clients when changes occurs on server. 2) `ServerToOwnerClient` Changes handle by server, will send to owner-client when changes occurs on server. 3) `ClientMulticast` Changes handle by owner-client, will send to server then server multicast to other clients when changes occurs on owner-client.
- `onChange(bool initial, TType oldValue, TType newValue)`, event when data changes on clients.


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
- `onOperation(LiteNetLibSyncListOp op, int itemIndex, TType oldItem, TType newItem)`, event when process operations on clients

Its supported types is like as `LiteNetLibSyncField` and also able to create custom types like it too, so you can do like this

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    private LiteNetLibSyncList<CharacterStats> stats = new LiteNetLibSyncList<CharacterStats>();
}
```

## How does it work?

State sync message will be sent if it has something changes (spawn/despawn/sync field/sync list) reliably every tick.
But sync fields will not be packed immediately, it will send updates to clients every ticks, unreliable. Then it will be packed together with state sync message if it has no change in the next tick.

### For example
```
Tick | Data
1001 | 1 send unreliably
1002 | 2 send unreliably
1003 | 2 no changes, pack with state sync message and send reliable
```