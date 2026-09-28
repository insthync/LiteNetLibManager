# LiteNetLibManager Editor test results — 2026-09-28

This is the complete result list for the `LiteNetLibManager.Tests` Unity EditMode assembly after the owner-transfer fix at submodule commit `9a03bf4`.

- Unity version: 2022.3.62f3
- Run window (UTC): 2026-09-28 11:08:34Z to 2026-09-28 11:08:34Z
- NUnit result: Passed
- Total: 94; passed: 94; failed: 0; inconclusive: 0; skipped: 0
- NUnit duration: 0.6816313 seconds (test execution; Unity startup and compilation are outside this figure)
- Source: [NUnit XML result](editor_test_results_2026-09-28.xml)
- Focused state-sync run: 23 passed, 0 failed, before the full assembly run.

All case names below are in the `LiteNetLibManager.Tests` namespace. Each row is one NUnit test-case result, including each parameterized case.

## Results by fixture

| Fixture | Passed | Total |
| --- | ---: | ---: |
| [GameManagerStateSyncTests](../../Tests/Editor/GameManagerStateSyncTests.cs) | 23 | 23 |
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

### GameManagerStateSyncTests (23)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BaselineReader_StopsWhenAnElementIsUnknown | Passed | 0.022815 |
| DeltaElementsThatFit_AreSplitIntoValidUnreliablePackets | Passed | 0.111394 |
| DeltaReader_RejectsAnInvalidLengthWithoutMovingPastThePacket | Passed | 0.002111 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraClientField,True) | Passed | 0.004836 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(ExtraServerField,False) | Passed | 0.003235 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(LeadingServerBehaviour,False) | Passed | 0.002343 |
| DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(TrailingServerBehaviour,True) | Passed | 0.001207 |
| GenericSyncField_NullToNullIsNotAChange | Passed | 0.000269 |
| InitialSyncFailure_RollsBackTheSpawnedObject | Passed | 0.001265 |
| ManagerDataStateAndGenericField_DoNotAllocateAfterWarmup | Passed | 0.003025 |
| OversizedDeltaElement_IsSentReliablyInstead | Passed | 0.002427 |
| OwnerTransfer_SendsCurrentOwnerOnlyValueToNewOwner(False) | Passed | 0.008444 |
| OwnerTransfer_SendsCurrentOwnerOnlyValueToNewOwner(True) | Passed | 0.001027 |
| PendingRpcs_AreCopiedAndBoundedByCountAndBytes | Passed | 0.002462 |
| PendingRpcs_ExpireWithoutAnObjectSpawn | Passed | 0.000425 |
| RapidOwnerTransfers_DiscardQueuedOwnerOnlyUpdatesForPreviousOwners | Passed | 0.001482 |
| RecycledStates_AreEmptyBeforeTheNextUpdate | Passed | 0.000665 |
| ServerBaseline_WritesOnlyNonEmptyStatesAndResetsThem | Passed | 0.000630 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,False) | Passed | 0.000802 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToClients,True) | Passed | 0.000723 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,False) | Passed | 0.002347 |
| ServerFieldModes_FilterInitialAndUpdateRecipients(ServerToOwnerClient,True) | Passed | 0.000951 |
| SyncStateUpdates_DoNotAllocateAfterWarmup | Passed | 0.001123 |

### GcAllocationTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| BuiltInSerializationAndCollectionWrites_DoNotAllocateAfterWarmup | Passed | 0.004544 |
| BuiltInValueCodecs_PreserveRegistryWireFormat | Passed | 0.018890 |
| CustomValueCodecs_StillOverrideBuiltInCodecs | Passed | 0.000820 |

### NetDataExtensionTests (20)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(-1) | Passed | 0.001454 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(65536) | Passed | 0.000132 |
| CollectionReaders_RejectInvalidCountsBeforeAllocating(int.MaxValue) | Passed | 0.000068 |
| CollectionWriters_RejectCountsBeyondReaderLimit | Passed | 0.001380 |
| Color_UsesFourQuantizedBytes | Passed | 0.000368 |
| Dictionary_HandlesValuesAndNullAsEmpty | Passed | 0.000575 |
| EnumValues_UseTheirUnderlyingWidthsAndReturnEnumValues | Passed | 0.001423 |
| GenericArray_HandlesValuesAndNullAsEmpty | Passed | 0.000472 |
| GenericValue_UsesRegisteredReaderAndWriter | Passed | 0.000115 |
| List_HandlesValuesAndNullAsEmpty | Passed | 0.000556 |
| ObjectArray_HandlesValuesAndNullAsEmpty | Passed | 0.000292 |
| Quaternion_WritesAndReadsComponentsInOrder | Passed | 0.000445 |
| SerializableValue_UsesFallbackForBothOverloads | Passed | 0.001905 |
| TypeValue_UsesRegisteredReaderAndWriter | Passed | 0.000201 |
| UnsupportedValue_ThrowsOnReadAndWrite | Passed | 0.001870 |
| Vector2_WritesAndReadsComponentsInOrder | Passed | 0.000514 |
| Vector2Int_WritesAndReadsComponentsInOrder | Passed | 0.000368 |
| Vector3_WritesAndReadsComponentsInOrder | Passed | 0.000372 |
| Vector3Int_WritesAndReadsComponentsInOrder | Passed | 0.000349 |
| Vector4_WritesAndReadsComponentsInOrder | Passed | 0.000513 |

### RequestFailureTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| FailedRequests_ReportUnimplementedWithoutSendingPreviousPacket | Passed | 0.008619 |

### RpcHashIdTests (6)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| GetHashedId_DistinctMethodIds_HaveNoCollisions | Passed | 0.001268 |
| GetHashedId_EmptyString_DoesNotThrow | Passed | 0.000295 |
| GetHashedId_IgnoresCharactersAfterNullTerminator | Passed | 0.000093 |
| GetHashedId_IsDeterministic | Passed | 0.000079 |
| GetHashedId_IsOrderSensitive | Passed | 0.000086 |
| GetHashedId_KnownIds_AreStableAcrossVersions | Passed | 0.000079 |

### SerializationRoundTripTests (18)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Bool_RoundTrips | Passed | 0.001292 |
| Byte_RoundTrips | Passed | 0.000179 |
| Char_RoundTrips | Passed | 0.000149 |
| Color_RoundTripsWithinQuantizationError | Passed | 0.000165 |
| Dictionary_RoundTrips | Passed | 0.001023 |
| Double_RoundTripsExactly | Passed | 0.000189 |
| Enum_RoundTrips | Passed | 0.000206 |
| Float_RoundTripsExactly | Passed | 0.000193 |
| FloatArray_RoundTripsExactly | Passed | 0.000185 |
| IntArray_RoundTrips | Passed | 0.000133 |
| NullArray_WritesEmptyArray | Passed | 0.000114 |
| Quaternion_RoundTripsExactly | Passed | 0.000340 |
| SByte_RoundTrips | Passed | 0.000198 |
| SignedIntegers_RoundTrips | Passed | 0.000249 |
| String_RoundTrips | Passed | 0.000226 |
| StringArray_RoundTrips | Passed | 0.000156 |
| UnsignedIntegers_RoundTrips | Passed | 0.000342 |
| Vectors_RoundTripsExactly | Passed | 0.000439 |

### ServerRestartTests (3)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| OfflineServerRestart_DropsQueuedPacketsFromPreviousRun | Passed | 0.000405 |
| StopServer_ClearsConnectionIds | Passed | 0.000656 |
| WebSocketServerRestart_DropsEventsFromPreviousRun | Passed | 0.065582 |

### SyncListOperationTests (10)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| Clear_ReplacesAllPriorOperations | Passed | 0.001810 |
| DeltaSync_Insert_RoundTrips | Passed | 0.000351 |
| DeltaSync_RoundTripsOperations | Passed | 0.000508 |
| Dirty_AtSameIndex_CoalescesPriorDirty | Passed | 0.000306 |
| InitialSync_RoundTripsFullState | Passed | 0.000173 |
| ReadSyncData_FiresOnOperationCallbacks | Passed | 0.000277 |
| RemoveAt_KeepsSetAtOtherIndexes | Passed | 0.000205 |
| Set_AtSameIndex_CoalescesPriorSet | Passed | 0.000159 |
| Set_ReplacesPriorDirtyAtSameIndex | Passed | 0.000167 |
| Synced_ClearsPendingOperationLog | Passed | 0.000151 |

### TransportHandlerPacketTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| TruncatedPacketHeaders_AreDroppedWithoutStoppingLaterPackets | Passed | 0.002364 |

### VarIntPackingTests (7)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| PackedInt_RoundTripsIncludingExtremes | Passed | 0.000450 |
| PackedLong_RoundTripsIncludingExtremes | Passed | 0.000180 |
| PackedShort_RoundTripsIncludingExtremes | Passed | 0.000146 |
| PackedUInt_RoundTrips | Passed | 0.000136 |
| PackedULong_RoundTripsAtEncodingBoundaries | Passed | 0.000223 |
| PackedUShort_RoundTripsIncludingExtremes | Passed | 0.000170 |
| SmallValues_StayCompact | Passed | 0.000200 |

### WebSocketClientSendTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| ReusedWriter_SendsOwnedPayloadsInOrder | Passed | 0.087268 |

### WebSocketServerLimitTests (1)

| Test case | Result | Duration (s) |
| --- | --- | ---: |
| AdditionalConnection_IsRejectedAtConfiguredLimit | Passed | 0.248660 |
