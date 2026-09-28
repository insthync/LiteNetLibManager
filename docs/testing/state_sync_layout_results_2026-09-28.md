# Game state syncing: usage, workflow, and layout results

This guide describes the current `LiteNetLibGameManager` state sync path. The [earlier Editor test results](editor_test_results_2026-09-28.md) record the packet-reader run (103 passed, 0 failed). The readiness layout tests and full run are in the [schema-check results](editor_test_results_2026-09-28_schema.md).

## How to use it

1. Put a `LiteNetLibIdentity` and a `LiteNetLibBehaviour` on each networked prefab or scene object. Register the same prefab asset on server and client, or give matching scene objects the same scene-object ID, so the client can resolve a spawn. Keep behaviour types, order, and synced field names and types consistent across both builds. The ClientReady layout check enforces this before state is sent.
2. Declare `LiteNetLibSyncField<T>` or `LiteNetLibSyncList<T>` members in the behaviour. The identity discovers these members when it sets up its behaviours. Configure callbacks in `OnSetup`, which runs before the sync elements are set up and before an initial value is read.
3. Spawn the object through the server's `LiteNetLibAssets.NetworkSpawn` flow. Set server-authoritative fields through `.Value` and change lists through their list methods. The manager queues the changes; application code does not call `WriteSyncElements` or send state packets directly.
4. On the client, read the synced values or react to `onChange` and `onOperation`. A field callback receives `initial = true` for initial state. Initial list entries arrive as `AddInitial` operations.

For example, attach this behaviour to a network object with `LiteNetLibIdentity`:

```csharp
using LiteNetLibManager;
using UnityEngine;

public sealed class ExampleState : LiteNetLibBehaviour
{
    [SerializeField] private SyncFieldInt health = new SyncFieldInt();
    [SerializeField] private SyncListInt itemIds = new SyncListInt();

    public int Health => health.Value;
    public int ItemCount => itemIds.Count;
    public int GetItemId(int index) => itemIds[index];

    public override void OnSetup()
    {
        health.syncMode = LiteNetLibSyncFieldMode.ServerToClients;
        health.onChange += OnHealthChanged;
        itemIds.forOwnerOnly = true;
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
        // React to the value, such as updating a health display.
    }

    private void OnItemOperation(LiteNetLibSyncListOp op, int index, int oldItem, int newItem)
    {
        // React to a list operation, such as updating an inventory display.
    }
}
```

Call `SetHealth` and `AddItem` on the spawned server object. These methods guard the writes; a spawned non-server client cannot write a default server-to-clients field or list. The callbacks can also run on the server when a value or list changes, so check `IsClient` inside a callback if it only updates client UI.

| Element | Who changes it in this example | Initial state | Later updates |
| --- | --- | --- | --- |
| `SyncFieldInt` with `ServerToClients` | Server | Current value in the spawn state | Unreliable delta while eligible; reliable baseline when scheduled |
| `SyncFieldInt` with `ServerToOwnerClient` | Server | Current value only in the subscribed owner's spawn state | Delta or baseline updates only to the subscribed owner |
| `SyncListInt` with `forOwnerOnly` | Server | Full list only in the owner's spawn state | Reliable operations only to the owner |

`ClientMulticast` is an available field mode for owner-originated values: the owner client queues a reliable update to the server, which applies it and queues forwarding to subscribed clients. Use the default server-to-clients mode for server-owned game state.

### Owner-only fields and lists

`ServerToOwnerClient` includes its field only in the subscribed owner's initial spawn state and sends later reliable or unreliable updates only to that owner. Other subscribers still receive the object spawn and any other fields they are allowed to see. The [four owner-recipient test cases](editor_test_results_2026-09-28.md#gamemanagerstatesynctests-30) cover both delivery paths and compare this mode with `ServerToClients`.

When the server changes an object's owner, it queues the current owner-only field values and a full replacement of owner-only lists for the new owner if that client already has the object. If the new owner's spawn is still pending, that spawn carries the values instead. The server also removes unsent owner-only updates queued for the previous owner. The [owner-transfer test cases](editor_test_results_2026-09-28.md#gamemanagerstatesynctests-30) cover both spawn states and rapid transfers before the next sync tick.

Set a list's `forOwnerOnly` flag in `OnSetup()` to restrict its initial contents and later operations to the subscribed owner. Set a field's `doNotSync` flag to keep it local while still allowing local value changes and callbacks. The send path rechecks both flags before serializing queued updates. Enabling owner-only delivery after another client has received a list cannot erase that client's earlier copy.

## How state sync works

### Layout check before state

After the online scene and network assets are initialized, the client sends a fingerprint of its registered prefab and scene-object layouts with `ClientReady`. The server compares it with its own fingerprint before calling `SetPlayerReady` to mark the player ready or spawn the player object. Its response carries the server fingerprint, which the client also checks. Missing headers and mismatches refuse readiness and the client connection; the server logs the local and remote fingerprints on a mismatch. This uses the ClientReady request and response payloads; baseline and delta state packet formats are unchanged.

The fingerprint includes registered IDs, behaviour types and order, and each sync field's name, declared and concrete type, and whether it is present. Register assets before a manual `SendClientReady()`. Keep `packetVersion` current for changes the fingerprint cannot see, such as custom serializers, RPC signatures, dynamically registered assets after readiness, or sync behaviour changed inside method bodies. Override `CalculateSyncSchemaFingerprint()` if custom object registries must be included. Both builds need the new ClientReady header; an older peer is refused.

```text
Server: spawn or change field/list -> queue per-object state
       -> each server update filters ready subscribers
       -> send reliable SyncBaseLine or unreliable SyncDelta
Client: receive message -> find or spawn identity -> read element IDs and values
       -> update fields/lists and invoke callbacks
```

### Spawn and initial state

The server's interest/subscription flow adds a spawn state for each player that should see an object. On a server update, `LiteNetLibGameManager` sends queued state to ready players as a `SyncBaseLine` message with `ReliableOrdered` delivery. A spawn record contains the scene or asset ID, transform, object ID, owner connection ID, and every sync element allowed for that subscribed player. Fields write their current value; lists write their full contents.

The client resolves the scene object or prefab, initializes its `LiteNetLibIdentity`, and reads the initial sync elements before completing `NetworkSpawn`. If an element cannot be read, the spawn fails and the partially spawned object is removed. Successful reads trigger field and list callbacks with their initial-state indicators.

### Changes after spawn

Changing a field value or a list registers that element as updating. On each server update, the manager checks ready players and subscription, groups eligible elements by sync channel and object ID, and chooses a delivery path:

| State | Delivery | What the client applies |
| --- | --- | --- |
| Spawn or destroy | Reliable ordered `SyncBaseLine` | Create or remove the network object |
| List operations and other elements without delta support | Reliable ordered `SyncBaseLine` | Apply operations to the existing object |
| Delta-capable field change | `SyncDelta` with unreliable delivery | Apply the value if its tick is newer |
| Pending field at a baseline interval, or a delta element too large for an unreliable packet | Reliable ordered `SyncBaseLine` | Apply the latest value |

`baseLineSyncInterval` defaults to one second. It governs when pending elements use the reliable baseline path; it does not send an unchanged full world snapshot every second. A changed delta-capable field is sent with a limited redundancy count while pending. The manager splits delta data into packets that fit its unreliable size limit and queues an oversized element for reliable delivery. The client reads object IDs and element IDs to locate the destination value or list; later field updates with an old tick are ignored.

### Object removal

When a player unsubscribes, or the server destroys an object, a destroy state is queued. The client receives it through the reliable baseline path and removes the corresponding spawned object.

Implementation: [`LiteNetLibGameManager.StateSyncing.cs`](../../Scripts/GameApi/LiteNetLibGameManager.StateSyncing.cs), [`LiteNetLibSyncField.cs`](../../Scripts/GameApi/LiteNetLibSyncField.cs), [`LiteNetLibSyncList.cs`](../../Scripts/GameApi/LiteNetLibSyncList.cs), and [`LiteNetLibPlayer.cs`](../../Scripts/GameApi/LiteNetLibPlayer.cs).

## Client/server layout test results

The four parameterized `GameManagerStateSyncTests.DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults` cases exercise the packet reader directly, bypassing the join sequence. They passed in the [earlier Unity EditMode run](editor_test_results_2026-09-28.md). The new `DifferentSyncLayouts_RejectClientReadyBeforePlayerSpawn` cases passed in the [schema-check run](editor_test_results_2026-09-28_schema.md). All four differences now fail the readiness check before initial state is sent, including the two cases where the raw packet reader could apply the shared value.

| Server and client layout difference | Raw initial-state reader | ClientReady check |
| --- | --- | --- |
| Client has one extra sync field; server sends only the shared field | Spawn succeeds; shared value is 77 | Rejects before player spawn |
| Server has one extra sync field; client has only the shared field | Spawn fails; object is removed from the spawned-object map and marked unspawned | Rejects before player spawn |
| Server has an extra behaviour before the shared behaviour | Spawn fails; object is removed from the spawned-object map and marked unspawned | Rejects before player spawn |
| Server has an extra behaviour after the shared behaviour | Spawn succeeds; shared value is 77 | Rejects before player spawn |

The test constructs separate server and client managers and identities in one Unity Editor process. It writes initial sync elements on the server side, then passes the bytes to the client's `NetworkSpawn`. For failed cases it checks that the spawn returns null and that the client object is no longer registered as spawned. For successful cases it checks the shared field's value and that the reader consumed all initial sync bytes.

The order of behaviours matters because a sync element ID is derived from the behaviour's type name, its index, and the field name in [`MakeSyncElementId`](../../Scripts/GameApi/LiteNetLibBehaviour.cs). An extra behaviour before the shared one changes its index and therefore its ID. An extra behaviour after it leaves that ID unchanged.

Additional tests confirm that matching layouts accept the ClientReady header and leave application data unread for custom hooks, an absent client header is rejected before player spawn, an absent server header makes the client refuse the connection, and a missing scene object changes the fingerprint. These are in-process Unity EditMode tests; they do not exercise a real network connection or builds with different compiled assemblies.

Source: [test implementation](../../Tests/Editor/GameManagerStateSyncTests.cs), [earlier NUnit XML](editor_test_results_2026-09-28.xml), and [schema-check NUnit XML](editor_test_results_2026-09-28_schema.xml).
