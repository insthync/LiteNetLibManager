# State sync layout compatibility results - 2026-09-28

The four parameterized `GameManagerStateSyncTests.DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults` cases passed in the [full Unity EditMode run](editor_test_results_2026-09-28.md). They exercise initial state sync from a server layout to a different client layout.

| Server and client layout difference | Expected client result | Observed test result |
| --- | --- | --- |
| Client has one extra sync field; server sends only the shared field | Spawn succeeds; shared value is 77 | Passed |
| Server has one extra sync field; client has only the shared field | Spawn fails; object is removed from the spawned-object map and marked unspawned | Passed |
| Server has an extra behaviour before the shared behaviour | Spawn fails; object is removed from the spawned-object map and marked unspawned | Passed |
| Server has an extra behaviour after the shared behaviour | Spawn succeeds; shared value is 77 | Passed |

The test constructs separate server and client managers and identities in one Unity Editor process. It writes initial sync elements on the server side, then passes the bytes to the client's `NetworkSpawn`. For failed cases it checks that the spawn returns null and that the client object is no longer registered as spawned. For successful cases it checks the shared field's value and that the reader consumed all initial sync bytes.

The order of behaviours matters because a sync element ID is derived from the behaviour's type name, its index, and the field name in [`MakeSyncElementId`](../../Scripts/GameApi/LiteNetLibBehaviour.cs). An extra behaviour before the shared one changes its index and therefore its ID. An extra behaviour after it leaves that ID unchanged.

These cases establish the tested initial-sync outcomes. They do not exercise a real network connection, builds with different compiled assemblies, delta updates for mismatched layouts, or every possible layout difference.

Source: [test implementation](../../Tests/Editor/GameManagerStateSyncTests.cs) and [NUnit XML result](editor_test_results_2026-09-28.xml).
