# LiteNetLibManager schema-check Editor test results — 2026-09-28

The full `LiteNetLibManager.Tests` Unity EditMode assembly passed after adding the ClientReady layout check.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 12:15:23Z to 12:15:24Z
- NUnit result: Passed
- Total: 113; passed: 113; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 0.7650712 seconds (test execution; Unity startup and compilation are outside this figure)
- Complete case-level results: [NUnit XML](editor_test_results_2026-09-28_schema.xml)

| Fixture | Passed | Total |
| --- | ---: | ---: |
| `GameManagerStateSyncTests` | 40 | 40 |
| `GcAllocationTests` | 3 | 3 |
| `NetDataExtensionTests` | 20 | 20 |
| `RequestFailureTests` | 1 | 1 |
| `RpcHashIdTests` | 6 | 6 |
| `SerializationRoundTripTests` | 18 | 18 |
| `ServerRestartTests` | 3 | 3 |
| `SyncListOperationTests` | 12 | 12 |
| `TransportHandlerPacketTests` | 1 | 1 |
| `VarIntPackingTests` | 7 | 7 |
| `WebSocketClientSendTests` | 1 | 1 |
| `WebSocketServerLimitTests` | 1 | 1 |

## New readiness layout cases

All ten new cases in [`GameManagerStateSyncTests`](../../Tests/Editor/GameManagerStateSyncTests.cs) passed:

| Test | Verified result |
| --- | --- |
| `MatchingSyncLayouts_AcceptClientReadySchemaAndPreserveCustomData` | Matching client and server layouts accept the header; custom request bytes remain available and the client accepts the response header. |
| `DifferentSyncLayouts_RejectClientReadyBeforePlayerSpawn` (four cases) | Extra client field, extra server field, leading server behaviour, and trailing server behaviour each return `Error` before readiness or player spawn. |
| `MissingClientReadySchemaHeader_IsRejectedBeforePlayerSpawn` | An older or malformed client request is rejected before readiness or spawn. |
| `MissingServerReadySchemaHeader_RefusesClientConnection` | A client refuses an older server response without the header. |
| `ClientReadyTimeout_DoesNotReadSchemaFromMissingResponse` | A timeout with no reader reaches the existing response path without dereferencing the reader. |
| `SceneObjectRegistry_DifferenceChangesSyncSchema` | A missing registered scene object changes the fingerprint. |
| `PrefabRegistrationOrder_DoesNotChangeSyncSchema` | Prefab insertion order does not change a matching fingerprint. |

The four earlier raw initial-state layout cases remain in the assembly and passed. Their reader-level results and the new readiness outcomes are compared in the [state sync guide](state_sync_layout_results_2026-09-28.md). The tests use separate managers and objects inside one Editor process; they do not exercise a live transport connection or differently compiled builds.
