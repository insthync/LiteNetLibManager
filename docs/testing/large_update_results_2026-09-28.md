# Large state update test results — 2026-09-28

The full `LiteNetLibManager.Tests` Unity EditMode assembly passed after the baseline packet and default interest-manager changes.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 12:34:34Z to 12:34:38Z
- NUnit result: Passed
- Total: 106; passed: 106; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 3.333484 seconds (test execution only)
- Complete case-level results: [NUnit XML](large_update_results_2026-09-28.xml)
- Focused state-sync run: 33 passed, 0 failed

| Fixture | Passed | Total |
| --- | ---: | ---: |
| `GameManagerStateSyncTests` | 33 | 33 |
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

## New cases

| Test | Result |
| --- | --- |
| `ManyServerBaselineStates_AreSplitIntoCompleteOrderedPackets` | 5,000 destroy states became **2** reliable ordered packets, **22,481** bytes total, with a **16,382** byte largest packet. Every object ID appeared once and in order; each packet stayed within the 16 KiB target. |
| `SingleOversizedServerBaselineState_RemainsIntact` | One state exceeding the target stayed in one packet and its value decoded completely. The target is therefore a batching limit, not a hard per-object limit. |
| `DefaultInterestUpdates_DoNotAllocateAfterWarmup` | The repeated default interest update recorded **zero** `GC.Alloc` samples after warmup. |

The GC case runs with no ready players to isolate the allocation previously caused by creating the temporary subscription set each update. It does not measure the cost of scanning a populated world. The packet cases use an in-process recording transport rather than a live UDP or WebSocket connection. See the [state sync guide](state_sync_layout_results_2026-09-28.md) for the workflow and the single-state size limit.
