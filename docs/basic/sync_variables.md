# Sync fields and lists

Declare sync elements as fields on a `LiteNetLibBehaviour`. The identity discovers them during setup. Use a concrete type such as `SyncFieldInt` or `SyncListInt` for Unity serialization, and configure callbacks or modes in `OnSetup()`.

```csharp
using LiteNetLibManager;
using UnityEngine;

public sealed class CharacterState : LiteNetLibBehaviour
{
    [SerializeField] private SyncFieldInt health = new SyncFieldInt();
    [SerializeField] private SyncFieldInt privateScore = new SyncFieldInt();
    [SerializeField] private SyncListInt itemIds = new SyncListInt();

    public int Health => health.Value;

    public override void OnSetup()
    {
        privateScore.syncMode = LiteNetLibSyncFieldMode.ServerToOwnerClient;
        itemIds.forOwnerOnly = true;
        health.onChange += OnHealthChanged;
        itemIds.onOperation += OnItemOperation;
    }

    public void SetHealth(int value)
    {
        if (IsServer)
            health.Value = value;
    }

    public void AddItem(int itemId)
    {
        if (IsServer)
            itemIds.Add(itemId);
    }

    private void OnHealthChanged(bool initial, int oldValue, int newValue)
    {
        // Update a display when the value arrives.
    }

    private void OnItemOperation(LiteNetLibSyncListOp op, int index, int oldItem, int newItem)
    {
        // Update a display when a list operation arrives.
    }
}
```

## Field modes

| `syncMode` | Writer | Subscribers that receive server state |
| --- | --- | --- |
| `ServerToClients` (default) | Server | All subscribed clients |
| `ServerToOwnerClient` | Server | Only the subscribed owner client |
| `ClientMulticast` | Owner client | The owner sends to the server, which forwards to other subscribed clients |

`ServerToOwnerClient` applies to the initial spawn state and later reliable or unreliable updates. Other subscribers can still receive the object and its public fields. Set a field's mode on both peers before the spawn is read, for example in `OnSetup()`. The `onChange(bool initial, T oldValue, T newValue)` callback runs when an initial value is applied or a value changes; it may also run during a local server change.

Set `doNotSync = true` for a field that should stay local. Its value can still change and invoke local callbacks, but neither the server nor an owner client sends it. If you turn syncing back on after spawn, change the value or call `MarkAsChanged()` on the authoritative peer to queue the current value.

For a custom struct implementing `INetSerializable`, use `SyncFieldNetSerializableStruct<T>`. Custom reference types can derive from `SyncFieldNetSerializableClass<T>` and implement `Construct()`. Both peers must serialize fields in the same order.

## Lists

`LiteNetLibSyncList<T>` is server controlled after spawn. Initial contents arrive with `AddInitial` operations; later changes arrive as reliable list operations. `onOperation(LiteNetLibSyncListOp op, int index, T oldItem, T newItem)` runs on receipt and for local server changes.

Set `forOwnerOnly = true` in `OnSetup()` to include a list only in the subscribed owner's spawn and later reliable updates. On ownership transfer, a new owner that already has the object receives a reliable full-list replacement; a pending spawn carries the full list instead. Set the flag before any private contents are sent: enabling it later cannot erase data already received by other clients.

## Delivery and layout

Spawns and list operations use reliable ordered baseline state. Eligible field changes can use an unreliable delta, with a reliable baseline at the configured interval or when a delta is too large. Keep behaviour types, discovery order, and synced field names and types consistent between server and client. The ClientReady layout check rejects differences before state delivery. Baseline packets do not frame individual elements, so an unknown element cannot be skipped safely if the check was bypassed.

See the [state sync guide](../testing/state_sync_layout_results_2026-09-28.md) for the full workflow and [Editor results](../testing/editor_test_results_2026-09-28.md) for the tested owner-only and layout cases.
