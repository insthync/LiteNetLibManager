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

- `syncMode` controls who may change a field and which subscribers receive it. `ServerToClients` is the default server-authoritative mode. `ServerToOwnerClient` sends initial and later field values only to the subscribed owner. `ClientMulticast` allows an owner client to send a value to the server for forwarding to subscribed clients. See the [state sync guide](../testing/state_sync_layout_results_2026-09-28.md#owner-only-fields-and-list-limitation).
- `onChange(bool initial, TType oldValue, TType newValue)` runs when an initial value is applied or a value changes. It can also run on the server when server code changes a field.


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

- `forOwnerOnly` is present on the list API, but the current send path does not read it. Do not use it to restrict delivery to the owner; see the [state sync guide](../testing/state_sync_layout_results_2026-09-28.md#owner-only-fields-and-list-limitation).
- `onOperation(LiteNetLibSyncListOp op, int itemIndex, TType oldItem, TType newItem)` runs for list operations on clients and when server code changes a list.

Its supported types is like as `LiteNetLibSyncField` and also able to create custom types like it too, so you can do like this

```
using LiteNetLibManager;
public class CustomNetBehaviour : LiteNetLibBehaviour {
    private LiteNetLibSyncList<CharacterStats> stats = new LiteNetLibSyncList<CharacterStats>();
}
```

## How does it work?

The manager sends spawn, destroy, and list operations through reliable ordered baseline messages. Eligible field changes can use unreliable delta messages, with a reliable baseline when the interval is reached or an element is too large for an unreliable packet. See the [state sync guide and tested layout results](../testing/state_sync_layout_results_2026-09-28.md) for the setup example and full workflow.
