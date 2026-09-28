# LiteNetLibManager Editor test results — 2026-09-28

This is the complete result list for the LiteNetLibManager.Tests Unity EditMode assembly at submodule commit 9ecd146.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 04:40:34–04:40:35
- NUnit result: Passed
- Total: 87; passed: 87; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 0.6693081 seconds (test execution; Unity startup and compilation are outside this figure)
- Source: [NUnit XML result](editor_test_results_2026-09-28.xml)

All case names below are in the LiteNetLibManager.Tests namespace. Each row is one NUnit test-case result, including each parameterized case.

## Results by fixture

| Fixture | Passed | Total |
| --- | ---: | ---: |
| [GameManagerStateSyncTests](../../Tests/Editor/GameManagerStateSyncTests.cs) | 16 | 16 |
| [GcAllocationTests](../../Tests/Editor/GcAllocationTests.cs) | 3 | 3 |
| [NetDataExtensionTests](../../Tests/Editor/NetDataExtensionTests.cs) | 20 | 20 |
| [RequestFailureTests](../../Tests/Editor/RequestFailureTests.cs) | 1 | 1 |
| [RpcHashIdTests](../../Tests/Editor/RpcHashIdTests.cs) | 6 | 6 |
| [SerializationRoundTripTests](../../Tests/Editor/SerializationRoundTripTests.cs) | 18 | 18 |
| [ServerRestartTests](../../Tests/Editor/ServerRestartTests.cs) | 3 | 3 |
| [SyncListOperationTests](../../Tests/Editor/SyncListOperationTests.cs) | 10 | 10 |
| [TransportHandlerPacketTests](../../Tests/Editor/TransportHandlerPacketTests.cs) | 1 | 1 |
| [VarIntPackingTests](../../Tests/Editor/VarIntPackingTests.cs) | 7 | 7 |
| [WebSocketClientSendTests](../../Tests/Editor/WebSocketClientSendTests.cs) | 1 | 1 |
| [WebSocketServerLimitTests](../../Tests/Editor/WebSocketServerLimitTests.cs) | 1 | 1 |

## Individual results

### GameManagerStateSyncTests (16)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BaselineReader_StopsWhenAnElementIsUnknown | Passed | 0.021985 |
| DeltaElementsThatFit_AreSplitIntoValidUnreliablePackets | Passed | 0.105368 |
| DeltaReader_RejectsAnInvalidLengthWithoutMovingPastThePacket | Passed | 0.001750 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraClientField,True) | Passed | 0.004693 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraServerField,False) | Passed | 0.003212 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(LeadingServerBehaviour,False) | Passed | 0.002354 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(TrailingServerBehaviour,True) | Passed | 0.001043 |
| GenericSyncField_NullToNullIsNotAChange | Passed | 0.000199 |
| InitialSyncFailure_RollsBackTheSpawnedObject | Passed | 0.001198 |
| ManagerDataStateAndGenericField_DoNotAllocateAfterWarmup | Passed | 0.003201 |
| OversizedDeltaElement_IsSentReliablyInstead | Passed | 0.002674 |
| PendingRpcs_AreCopiedAndBoundedByCountAndBytes | Passed | 0.002185 |
| PendingRpcs_ExpireWithoutAnObjectSpawn | Passed | 0.000361 |
| RecycledStates_AreEmptyBeforeTheNextUpdate | Passed | 0.000622 |
| ServerBaseline_WritesOnlyNonEmptyStatesAndResetsThem | Passed | 0.000651 |
| SyncStateUpdates_DoNotAllocateAfterWarmup | Passed | 0.001111 |

### GcAllocationTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BuiltInSerializationAndCollectionWrites_DoNotAllocateAfterWarmup | Passed | 0.004283 |
| BuiltInValueCodecs_PreserveRegistryWireFormat | Passed | 0.018811 |
| CustomValueCodecs_StillOverrideBuiltInCodecs | Passed | 0.000838 |

### NetDataExtensionTests (20)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(-1) | Passed | 0.001392 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(65536) | Passed | 0.000110 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(int.MaxValue) | Passed | 0.000066 |
| CollectionWriters_RejectCountsBeyondReaderLimit | Passed | 0.001620 |
| Color_UsesFourQuantizedBytes | Passed | 0.000352 |
| Dictionary_HandlesValuesAndNullAsEmpty | Passed | 0.000897 |
| EnumValues_UseTheirUnderlyingWidthsAndReturnEnumValues | Passed | 0.001540 |
| GenericArray_HandlesValuesAndNullAsEmpty | Passed | 0.000644 |
| GenericValue_UsesRegisteredReaderAndWriter | Passed | 0.000317 |
| List_HandlesValuesAndNullAsEmpty | Passed | 0.000811 |
| ObjectArray_HandlesValuesAndNullAsEmpty | Passed | 0.000326 |
| Quaternion_WritesAndReadsComponentsInOrder | Passed | 0.000480 |
| SerializableValue_UsesFallbackForBothOverloads | Passed | 0.001960 |
| TypeValue_UsesRegisteredReaderAndWriter | Passed | 0.000207 |
| UnsupportedValue_ThrowsOnReadAndWrite | Passed | 0.001850 |
| Vector2_WritesAndReadsComponentsInOrder | Passed | 0.000534 |
| Vector2Int_WritesAndReadsComponentsInOrder | Passed | 0.000352 |
| Vector3_WritesAndReadsComponentsInOrder | Passed | 0.000387 |
| Vector3Int_WritesAndReadsComponentsInOrder | Passed | 0.000346 |
| Vector4_WritesAndReadsComponentsInOrder | Passed | 0.000404 |

### RequestFailureTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| FailedRequests_ReportUnimplementedWithoutSendingPreviousPacket | Passed | 0.008787 |

### RpcHashIdTests (6)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| GetHashedId_DistinctMethodIds_HaveNoCollisions | Passed | 0.001354 |
| GetHashedId_EmptyString_DoesNotThrow | Passed | 0.000265 |
| GetHashedId_IgnoresCharactersAfterNullTerminator | Passed | 0.000163 |
| GetHashedId_IsDeterministic | Passed | 0.000087 |
| GetHashedId_IsOrderSensitive | Passed | 0.000076 |
| GetHashedId_KnownIds_AreStableAcrossVersions | Passed | 0.000077 |

### SerializationRoundTripTests (18)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Bool_RoundTrips | Passed | 0.001089 |
| Byte_RoundTrips | Passed | 0.000173 |
| Char_RoundTrips | Passed | 0.000183 |
| Color_RoundTripsWithinQuantizationError | Passed | 0.000167 |
| Dictionary_RoundTrips | Passed | 0.000899 |
| Double_RoundTripsExactly | Passed | 0.000212 |
| Enum_RoundTrips | Passed | 0.000149 |
| Float_RoundTripsExactly | Passed | 0.000178 |
| FloatArray_RoundTripsExactly | Passed | 0.000183 |
| IntArray_RoundTrips | Passed | 0.000140 |
| NullArray_WritesEmptyArray | Passed | 0.000114 |
| Quaternion_RoundTripsExactly | Passed | 0.000483 |
| SByte_RoundTrips | Passed | 0.000190 |
| SignedIntegers_RoundTrips | Passed | 0.000247 |
| String_RoundTrips | Passed | 0.000351 |
| StringArray_RoundTrips | Passed | 0.000164 |
| UnsignedIntegers_RoundTrips | Passed | 0.000370 |
| Vectors_RoundTripsExactly | Passed | 0.000444 |

### ServerRestartTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| OfflineServerRestart_DropsQueuedPacketsFromPreviousRun | Passed | 0.000375 |
| StopServer_ClearsConnectionIds | Passed | 0.001090 |
| WebSocketServerRestart_DropsEventsFromPreviousRun | Passed | 0.065290 |

### SyncListOperationTests (10)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Clear_ReplacesAllPriorOperations | Passed | 0.002252 |
| DeltaSync_Insert_RoundTrips | Passed | 0.000372 |
| DeltaSync_RoundTripsOperations | Passed | 0.000571 |
| Dirty_AtSameIndex_CoalescesPriorDirty | Passed | 0.000310 |
| InitialSync_RoundTripsFullState | Passed | 0.000171 |
| ReadSyncData_FiresOnOperationCallbacks | Passed | 0.000201 |
| RemoveAt_KeepsSetAtOtherIndexes | Passed | 0.000159 |
| Set_AtSameIndex_CoalescesPriorSet | Passed | 0.000149 |
| Set_ReplacesPriorDirtyAtSameIndex | Passed | 0.000142 |
| Synced_ClearsPendingOperationLog | Passed | 0.000116 |

### TransportHandlerPacketTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| TruncatedPacketHeaders_AreDroppedWithoutStoppingLaterPackets | Passed | 0.002114 |

### VarIntPackingTests (7)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| PackedInt_RoundTripsIncludingExtremes | Passed | 0.000301 |
| PackedLong_RoundTripsIncludingExtremes | Passed | 0.000178 |
| PackedShort_RoundTripsIncludingExtremes | Passed | 0.000142 |
| PackedUInt_RoundTrips | Passed | 0.000134 |
| PackedULong_RoundTripsAtEncodingBoundaries | Passed | 0.000210 |
| PackedUShort_RoundTripsIncludingExtremes | Passed | 0.000175 |
| SmallValues_StayCompact | Passed | 0.000211 |

### WebSocketClientSendTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| ReusedWriter_SendsOwnedPayloadsInOrder | Passed | 0.087607 |

### WebSocketServerLimitTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| AdditionalConnection_IsRejectedAtConfiguredLimit | Passed | 0.248696 |

## Scope

These are Unity EditMode results for the selected test assembly. The allocation assertions in this suite check their specified operations after warmup; they do not measure total GC allocation for a running game. The state sync layout cases use separate manager and entity objects inside one Editor process and transfer a serialized initial state directly to a reader.
