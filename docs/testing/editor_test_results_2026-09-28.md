# LiteNetLibManager Editor test results — 2026-09-28

This is the complete result list for the LiteNetLibManager.Tests Unity EditMode assembly at submodule commit 4248445.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 05:07:55Z to 2026-09-28 05:07:56Z
- NUnit result: Passed
- Total: 91; passed: 91; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 0.6720554 seconds (test execution; Unity startup and compilation are outside this figure)
- Source: [NUnit XML result](editor_test_results_2026-09-28.xml)

All case names below are in the LiteNetLibManager.Tests namespace. Each row is one NUnit test-case result, including each parameterized case.

## Results by fixture

| Fixture | Passed | Total |
| --- | ---: | ---: |
| [GameManagerStateSyncTests](../../Tests/Editor/GameManagerStateSyncTests.cs) | 20 | 20 |
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

### GameManagerStateSyncTests (20)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BaselineReader_StopsWhenAnElementIsUnknown | Passed | 0.023160 |
| DeltaElementsThatFit_AreSplitIntoValidUnreliablePackets | Passed | 0.102641 |
| DeltaReader_RejectsAnInvalidLengthWithoutMovingPastThePacket | Passed | 0.002046 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraClientField,True) | Passed | 0.004793 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraServerField,False) | Passed | 0.003349 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(LeadingServerBehaviour,False) | Passed | 0.002710 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(TrailingServerBehaviour,True) | Passed | 0.001092 |
| GenericSyncField_NullToNullIsNotAChange | Passed | 0.000216 |
| InitialSyncFailure_RollsBackTheSpawnedObject | Passed | 0.001239 |
| ManagerDataStateAndGenericField_DoNotAllocateAfterWarmup | Passed | 0.003110 |
| OversizedDeltaElement_IsSentReliablyInstead | Passed | 0.002337 |
| PendingRpcs_AreCopiedAndBoundedByCountAndBytes | Passed | 0.002395 |
| PendingRpcs_ExpireWithoutAnObjectSpawn | Passed | 0.000432 |
| RecycledStates_AreEmptyBeforeTheNextUpdate | Passed | 0.000641 |
| ServerBaseline_WritesOnlyNonEmptyStatesAndResetsThem | Passed | 0.000619 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,False) | Passed | 0.009165 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,True) | Passed | 0.000953 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,False) | Passed | 0.000761 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,True) | Passed | 0.000613 |
| SyncStateUpdates_DoNotAllocateAfterWarmup | Passed | 0.001012 |

### GcAllocationTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BuiltInSerializationAndCollectionWrites_DoNotAllocateAfterWarmup | Passed | 0.004186 |
| BuiltInValueCodecs_PreserveRegistryWireFormat | Passed | 0.017497 |
| CustomValueCodecs_StillOverrideBuiltInCodecs | Passed | 0.000931 |

### NetDataExtensionTests (20)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(-1) | Passed | 0.001467 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(65536) | Passed | 0.000159 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(int.MaxValue) | Passed | 0.000065 |
| CollectionWriters_RejectCountsBeyondReaderLimit | Passed | 0.001605 |
| Color_UsesFourQuantizedBytes | Passed | 0.000391 |
| Dictionary_HandlesValuesAndNullAsEmpty | Passed | 0.000603 |
| EnumValues_UseTheirUnderlyingWidthsAndReturnEnumValues | Passed | 0.001755 |
| GenericArray_HandlesValuesAndNullAsEmpty | Passed | 0.000850 |
| GenericValue_UsesRegisteredReaderAndWriter | Passed | 0.000142 |
| List_HandlesValuesAndNullAsEmpty | Passed | 0.000568 |
| ObjectArray_HandlesValuesAndNullAsEmpty | Passed | 0.000301 |
| Quaternion_WritesAndReadsComponentsInOrder | Passed | 0.000434 |
| SerializableValue_UsesFallbackForBothOverloads | Passed | 0.001816 |
| TypeValue_UsesRegisteredReaderAndWriter | Passed | 0.000248 |
| UnsupportedValue_ThrowsOnReadAndWrite | Passed | 0.001975 |
| Vector2_WritesAndReadsComponentsInOrder | Passed | 0.000467 |
| Vector2Int_WritesAndReadsComponentsInOrder | Passed | 0.000391 |
| Vector3_WritesAndReadsComponentsInOrder | Passed | 0.000362 |
| Vector3Int_WritesAndReadsComponentsInOrder | Passed | 0.000329 |
| Vector4_WritesAndReadsComponentsInOrder | Passed | 0.000442 |

### RequestFailureTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| FailedRequests_ReportUnimplementedWithoutSendingPreviousPacket | Passed | 0.008368 |

### RpcHashIdTests (6)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| GetHashedId_DistinctMethodIds_HaveNoCollisions | Passed | 0.001058 |
| GetHashedId_EmptyString_DoesNotThrow | Passed | 0.000229 |
| GetHashedId_IgnoresCharactersAfterNullTerminator | Passed | 0.000083 |
| GetHashedId_IsDeterministic | Passed | 0.000073 |
| GetHashedId_IsOrderSensitive | Passed | 0.000101 |
| GetHashedId_KnownIds_AreStableAcrossVersions | Passed | 0.000086 |

### SerializationRoundTripTests (18)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Bool_RoundTrips | Passed | 0.001312 |
| Byte_RoundTrips | Passed | 0.000193 |
| Char_RoundTrips | Passed | 0.000156 |
| Color_RoundTripsWithinQuantizationError | Passed | 0.000162 |
| Dictionary_RoundTrips | Passed | 0.000887 |
| Double_RoundTripsExactly | Passed | 0.000185 |
| Enum_RoundTrips | Passed | 0.000135 |
| Float_RoundTripsExactly | Passed | 0.000162 |
| FloatArray_RoundTripsExactly | Passed | 0.000166 |
| IntArray_RoundTrips | Passed | 0.000124 |
| NullArray_WritesEmptyArray | Passed | 0.000107 |
| Quaternion_RoundTripsExactly | Passed | 0.000424 |
| SByte_RoundTrips | Passed | 0.000347 |
| SignedIntegers_RoundTrips | Passed | 0.000244 |
| String_RoundTrips | Passed | 0.000205 |
| StringArray_RoundTrips | Passed | 0.000151 |
| UnsignedIntegers_RoundTrips | Passed | 0.000243 |
| Vectors_RoundTripsExactly | Passed | 0.000446 |

### ServerRestartTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| OfflineServerRestart_DropsQueuedPacketsFromPreviousRun | Passed | 0.000378 |
| StopServer_ClearsConnectionIds | Passed | 0.000682 |
| WebSocketServerRestart_DropsEventsFromPreviousRun | Passed | 0.066074 |

### SyncListOperationTests (10)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Clear_ReplacesAllPriorOperations | Passed | 0.002062 |
| DeltaSync_Insert_RoundTrips | Passed | 0.000380 |
| DeltaSync_RoundTripsOperations | Passed | 0.000573 |
| Dirty_AtSameIndex_CoalescesPriorDirty | Passed | 0.000338 |
| InitialSync_RoundTripsFullState | Passed | 0.000197 |
| ReadSyncData_FiresOnOperationCallbacks | Passed | 0.000229 |
| RemoveAt_KeepsSetAtOtherIndexes | Passed | 0.000177 |
| Set_AtSameIndex_CoalescesPriorSet | Passed | 0.000167 |
| Set_ReplacesPriorDirtyAtSameIndex | Passed | 0.000166 |
| Synced_ClearsPendingOperationLog | Passed | 0.000135 |

### TransportHandlerPacketTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| TruncatedPacketHeaders_AreDroppedWithoutStoppingLaterPackets | Passed | 0.002239 |

### VarIntPackingTests (7)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| PackedInt_RoundTripsIncludingExtremes | Passed | 0.000377 |
| PackedLong_RoundTripsIncludingExtremes | Passed | 0.000184 |
| PackedShort_RoundTripsIncludingExtremes | Passed | 0.000145 |
| PackedUInt_RoundTrips | Passed | 0.000140 |
| PackedULong_RoundTripsAtEncodingBoundaries | Passed | 0.000218 |
| PackedUShort_RoundTripsIncludingExtremes | Passed | 0.000162 |
| SmallValues_StayCompact | Passed | 0.000239 |

### WebSocketClientSendTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| ReusedWriter_SendsOwnedPayloadsInOrder | Passed | 0.086541 |

### WebSocketServerLimitTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| AdditionalConnection_IsRejectedAtConfiguredLimit | Passed | 0.248575 |

## Scope

These are Unity EditMode results for the selected test assembly. The allocation assertions in this suite check their specified operations after warmup; they do not measure total GC allocation for a running game. The state sync layout cases use separate manager and entity objects inside one Editor process and transfer a serialized initial state directly to a reader. The owner-only field cases use two subscribed test players and inspect the initial, reliable baseline, and unreliable delta packets sent to each recipient.
