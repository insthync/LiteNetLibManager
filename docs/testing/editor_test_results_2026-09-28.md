# LiteNetLibManager Editor test results — 2026-09-28

This is the complete result list for the `LiteNetLibManager.Tests` Unity EditMode assembly after sync flag fixes at submodule commits `8f59909` and `90f9a01`.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 11:25:52Z to 2026-09-28 11:25:53Z
- NUnit result: Passed
- Total: 103; passed: 103; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 0.671927 seconds (test execution; Unity startup and compilation are outside this figure)
- Source: [NUnit XML result](editor_test_results_2026-09-28.xml)
- Focused state-sync run: 30 passed, 0 failed, before the final full assembly run.

All case names below are in the `LiteNetLibManager.Tests` namespace. Each row is one NUnit test-case result, including each parameterized case.

## Results by fixture

| Fixture | Passed | Total |
| --- | ---: | ---: |
| [GameManagerStateSyncTests](../../Tests/Editor/GameManagerStateSyncTests.cs) | 30 | 30 |
| [GcAllocationTests](../../Tests/Editor/GcAllocationTests.cs) | 3 | 3 |
| [NetDataExtensionTests](../../Tests/Editor/NetDataExtensionTests.cs) | 20 | 20 |
| [RequestFailureTests](../../Tests/Editor/RequestFailureTests.cs) | 1 | 1 |
| [RpcHashIdTests](../../Tests/Editor/RpcHashIdTests.cs) | 6 | 6 |
| [SerializationRoundTripTests](../../Tests/Editor/SerializationRoundTripTests.cs) | 18 | 18 |
| [ServerRestartTests](../../Tests/Editor/ServerRestartTests.cs) | 3 | 3 |
| [SyncListOperationTests](../../Tests/Editor/SyncListOperationTests.cs) | 12 | 12 |
| [TransportHandlerPacketTests](../../Tests/Editor/TransportHandlerPacketTests.cs) | 1 | 1 |
| [VarIntPackingTests](../../Tests/Editor/VarIntPackingTests.cs) | 7 | 7 |
| [WebSocketClientSendTests](../../Tests/Editor/WebSocketClientSendTests.cs) | 1 | 1 |
| [WebSocketServerLimitTests](../../Tests/Editor/WebSocketServerLimitTests.cs) | 1 | 1 |

### GameManagerStateSyncTests (30)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BaselineReader_StopsWhenAnElementIsUnknown | Passed | 0.026111 |
| DeltaElementsThatFit_AreSplitIntoValidUnreliablePackets | Passed | 0.112576 |
| DeltaReader_RejectsAnInvalidLengthWithoutMovingPastThePacket | Passed | 0.001623 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraClientField,True) | Passed | 0.004733 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraServerField,False) | Passed | 0.003683 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(LeadingServerBehaviour,False) | Passed | 0.003313 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(TrailingServerBehaviour,True) | Passed | 0.001305 |
| DoNotSyncField_StaysLocalDuringSpawnAndQueuedUpdates | Passed | 0.011523 |
| GenericSyncField_NullToNullIsNotAChange | Passed | 0.000299 |
| InitialSyncFailure_RollsBackTheSpawnedObject | Passed | 0.001678 |
| ManagerDataStateAndGenericField_DoNotAllocateAfterWarmup | Passed | 0.002618 |
| OversizedDeltaElement_IsSentReliablyInstead | Passed | 0.001517 |
| OwnerOnlyList_FiltersAnUpdateQueuedBeforeFlagChanged | Passed | 0.003913 |
| OwnerOnlyList_FiltersSpawnAndUpdateRecipients(False) | Passed | 0.001582 |
| OwnerOnlyList_FiltersSpawnAndUpdateRecipients(True) | Passed | 0.000703 |
| OwnerOnlyList_RapidTransfersDiscardIntermediateFullState | Passed | 0.002250 |
| OwnerOnlyList_TransfersCurrentContentsToNewOwner(False) | Passed | 0.001279 |
| OwnerOnlyList_TransfersCurrentContentsToNewOwner(True) | Passed | 0.000646 |
| OwnerTransfer_SendsCurrentOwnerOnlyValueToNewOwner(False) | Passed | 0.001366 |
| OwnerTransfer_SendsCurrentOwnerOnlyValueToNewOwner(True) | Passed | 0.000652 |
| PendingRpcs_AreCopiedAndBoundedByCountAndBytes | Passed | 0.002549 |
| PendingRpcs_ExpireWithoutAnObjectSpawn | Passed | 0.000503 |
| RapidOwnerTransfers_DiscardQueuedOwnerOnlyUpdatesForPreviousOwners | Passed | 0.001490 |
| RecycledStates_AreEmptyBeforeTheNextUpdate | Passed | 0.000892 |
| ServerBaseline_WritesOnlyNonEmptyStatesAndResetsThem | Passed | 0.000861 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,False) | Passed | 0.000615 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,True) | Passed | 0.000613 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,False) | Passed | 0.002254 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,True) | Passed | 0.000689 |
| SyncStateUpdates_DoNotAllocateAfterWarmup | Passed | 0.001146 |

### GcAllocationTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BuiltInSerializationAndCollectionWrites_DoNotAllocateAfterWarmup | Passed | 0.004309 |
| BuiltInValueCodecs_PreserveRegistryWireFormat | Passed | 0.019809 |
| CustomValueCodecs_StillOverrideBuiltInCodecs | Passed | 0.000968 |

### NetDataExtensionTests (20)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(-1) | Passed | 0.001514 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(65536) | Passed | 0.000483 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(int.MaxValue) | Passed | 0.000102 |
| CollectionWriters_RejectCountsBeyondReaderLimit | Passed | 0.001940 |
| Color_UsesFourQuantizedBytes | Passed | 0.000585 |
| Dictionary_HandlesValuesAndNullAsEmpty | Passed | 0.000785 |
| EnumValues_UseTheirUnderlyingWidthsAndReturnEnumValues | Passed | 0.001600 |
| GenericArray_HandlesValuesAndNullAsEmpty | Passed | 0.000675 |
| GenericValue_UsesRegisteredReaderAndWriter | Passed | 0.000170 |
| List_HandlesValuesAndNullAsEmpty | Passed | 0.000603 |
| ObjectArray_HandlesValuesAndNullAsEmpty | Passed | 0.000315 |
| Quaternion_WritesAndReadsComponentsInOrder | Passed | 0.000528 |
| SerializableValue_UsesFallbackForBothOverloads | Passed | 0.001838 |
| TypeValue_UsesRegisteredReaderAndWriter | Passed | 0.000156 |
| UnsupportedValue_ThrowsOnReadAndWrite | Passed | 0.001977 |
| Vector2_WritesAndReadsComponentsInOrder | Passed | 0.000456 |
| Vector2Int_WritesAndReadsComponentsInOrder | Passed | 0.000365 |
| Vector3_WritesAndReadsComponentsInOrder | Passed | 0.000364 |
| Vector3Int_WritesAndReadsComponentsInOrder | Passed | 0.000335 |
| Vector4_WritesAndReadsComponentsInOrder | Passed | 0.000399 |

### RequestFailureTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| FailedRequests_ReportUnimplementedWithoutSendingPreviousPacket | Passed | 0.008204 |

### RpcHashIdTests (6)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| GetHashedId_DistinctMethodIds_HaveNoCollisions | Passed | 0.001098 |
| GetHashedId_EmptyString_DoesNotThrow | Passed | 0.000273 |
| GetHashedId_IgnoresCharactersAfterNullTerminator | Passed | 0.000090 |
| GetHashedId_IsDeterministic | Passed | 0.000081 |
| GetHashedId_IsOrderSensitive | Passed | 0.000073 |
| GetHashedId_KnownIds_AreStableAcrossVersions | Passed | 0.000074 |

### SerializationRoundTripTests (18)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Bool_RoundTrips | Passed | 0.001098 |
| Byte_RoundTrips | Passed | 0.000177 |
| Char_RoundTrips | Passed | 0.000157 |
| Color_RoundTripsWithinQuantizationError | Passed | 0.000171 |
| Dictionary_RoundTrips | Passed | 0.000939 |
| Double_RoundTripsExactly | Passed | 0.000193 |
| Enum_RoundTrips | Passed | 0.000230 |
| Float_RoundTripsExactly | Passed | 0.000171 |
| FloatArray_RoundTripsExactly | Passed | 0.000167 |
| IntArray_RoundTrips | Passed | 0.000129 |
| NullArray_WritesEmptyArray | Passed | 0.000113 |
| Quaternion_RoundTripsExactly | Passed | 0.000339 |
| SByte_RoundTrips | Passed | 0.000200 |
| SignedIntegers_RoundTrips | Passed | 0.000275 |
| String_RoundTrips | Passed | 0.000212 |
| StringArray_RoundTrips | Passed | 0.000148 |
| UnsignedIntegers_RoundTrips | Passed | 0.000230 |
| Vectors_RoundTripsExactly | Passed | 0.000424 |

### ServerRestartTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| OfflineServerRestart_DropsQueuedPacketsFromPreviousRun | Passed | 0.000368 |
| StopServer_ClearsConnectionIds | Passed | 0.000584 |
| WebSocketServerRestart_DropsEventsFromPreviousRun | Passed | 0.032579 |

### SyncListOperationTests (12)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Clear_ReplacesAllPriorOperations | Passed | 0.000960 |
| DeltaSync_Insert_RoundTrips | Passed | 0.000323 |
| DeltaSync_RoundTripsOperations | Passed | 0.000663 |
| Dirty_AtSameIndex_CoalescesPriorDirty | Passed | 0.000336 |
| FullStateAsOperations_ReplacesPreviousOwnerContents(False) | Passed | 0.000261 |
| FullStateAsOperations_ReplacesPreviousOwnerContents(True) | Passed | 0.000094 |
| InitialSync_RoundTripsFullState | Passed | 0.000224 |
| ReadSyncData_FiresOnOperationCallbacks | Passed | 0.000223 |
| RemoveAt_KeepsSetAtOtherIndexes | Passed | 0.000228 |
| Set_AtSameIndex_CoalescesPriorSet | Passed | 0.000175 |
| Set_ReplacesPriorDirtyAtSameIndex | Passed | 0.000151 |
| Synced_ClearsPendingOperationLog | Passed | 0.000124 |

### TransportHandlerPacketTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| TruncatedPacketHeaders_AreDroppedWithoutStoppingLaterPackets | Passed | 0.002308 |

### VarIntPackingTests (7)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| PackedInt_RoundTripsIncludingExtremes | Passed | 0.000348 |
| PackedLong_RoundTripsIncludingExtremes | Passed | 0.000269 |
| PackedShort_RoundTripsIncludingExtremes | Passed | 0.000164 |
| PackedUInt_RoundTrips | Passed | 0.000189 |
| PackedULong_RoundTripsAtEncodingBoundaries | Passed | 0.000229 |
| PackedUShort_RoundTripsIncludingExtremes | Passed | 0.000205 |
| SmallValues_StayCompact | Passed | 0.000275 |

### WebSocketClientSendTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| ReusedWriter_SendsOwnedPayloadsInOrder | Passed | 0.087627 |

### WebSocketServerLimitTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| AdditionalConnection_IsRejectedAtConfiguredLimit | Passed | 0.248666 |
