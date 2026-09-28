using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiteNetLibManager.Tests
{
    public class GameManagerStateSyncTests
    {
        public enum LayoutDifference
        {
            ExtraClientField,
            ExtraServerField,
            LeadingServerBehaviour,
            TrailingServerBehaviour,
        }

        private const int Iterations = 256;
        private delegate bool WriteServerState(NetDataWriter writer, LiteNetLibPlayer player,
            uint objectId, GameStateSyncData state, uint tick);

        private static void AssertListStatePacket(RecordingGameTransport.SentPacket sent, int elementId,
            bool spawn, long ownerId, bool fullResync, params int[] values)
        {
            var reader = new NetDataReader(sent.Data);
            Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
            reader.GetPackedUInt(); // tick
            Assert.AreEqual(1, reader.GetUShort());
            Assert.AreEqual(spawn ? GameStateSyncType.Spawn : GameStateSyncType.Data, (GameStateSyncType)reader.GetByte());
            if (spawn)
            {
                Assert.IsFalse(reader.GetBool());
                reader.GetPackedInt(); // asset ID
                for (int i = 0; i < 6; ++i)
                    reader.GetFloat(); // position and rotation
            }
            Assert.AreEqual(42u, reader.GetPackedUInt());
            if (spawn)
                Assert.AreEqual(ownerId, reader.GetPackedLong());
            Assert.AreEqual(values.Length > 0 ? 1 : 0, reader.GetPackedInt());
            if (values.Length > 0)
            {
                Assert.AreEqual(elementId, reader.GetPackedInt());
                if (spawn)
                {
                    Assert.AreEqual(values.Length, reader.GetPackedInt());
                    foreach (int value in values)
                        Assert.AreEqual(value, reader.GetPackedInt());
                }
                else
                {
                    Assert.AreEqual(values.Length + (fullResync ? 1 : 0), reader.GetPackedInt());
                    if (fullResync)
                        Assert.AreEqual(LiteNetLibSyncListOp.Clear, (LiteNetLibSyncListOp)reader.GetByte());
                    foreach (int value in values)
                    {
                        Assert.AreEqual(LiteNetLibSyncListOp.Add, (LiteNetLibSyncListOp)reader.GetByte());
                        Assert.AreEqual(value, reader.GetPackedInt());
                    }
                }
            }
            Assert.IsTrue(reader.EndOfData);
        }

        private static long AllocationCount(Action action)
        {
            for (int i = 0; i < 32; ++i)
                action();

            using (var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                for (int i = 0; i < Iterations; ++i)
                    action();
                recorder.Stop();
                return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
            }
        }

        [Test]
        public void SyncStateUpdates_DoNotAllocateAfterWarmup()
        {
            object allocated = null;
            long positiveControl = AllocationCount(() => allocated = new byte[16]);
            var baseline = new LiteNetLibSyncingStates();
            long baselineAllocations = AllocationCount(() =>
            {
                baseline.ClearChannel(1);
                baseline.PrepareSyncStateData(1, 42);
            });
            long baselineResetAllocations = AllocationCount(() =>
            {
                baseline.Clear();
                baseline.PrepareSyncStateData(1, 42);
            });

            var gameObject = new GameObject("state sync test");
            try
            {
                gameObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = gameObject.AddComponent<LiteNetLibBehaviour>();
                var field = new LiteNetLibSyncField<int>();
                typeof(LiteNetLibElement).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(field, new object[] { behaviour, 123 });

                var delta = new LiteNetLibSyncingDeltaStates();
                long deltaAllocations = AllocationCount(() =>
                {
                    delta.Clear();
                    delta.AppendDataSyncState(field);
                });

                Assert.Greater(positiveControl, 0, "The GC.Alloc recorder must detect a known allocation");
                Assert.Zero(baselineAllocations, $"Baseline state allocated {baselineAllocations} times in {Iterations} updates");
                Assert.Zero(baselineResetAllocations, $"Baseline reset allocated {baselineResetAllocations} times in {Iterations} updates");
                Assert.Zero(deltaAllocations, $"Delta state allocated {deltaAllocations} times in {Iterations} updates");
                GC.KeepAlive(allocated);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RecycledStates_AreEmptyBeforeTheNextUpdate()
        {
            var states = new LiteNetLibSyncingStates();
            var first = states.PrepareSyncStateData(1, 42);
            first.StateType = GameStateSyncType.Destroy;
            first.DestroyReasons = 7;
            var list = new SyncListInt();
            first.MarkFullListSync(list);
            states.ClearChannel(1);

            Assert.IsEmpty(states.States[1]);
            var recycled = states.PrepareSyncStateData(1, 42);
            Assert.AreSame(first, recycled);
            Assert.AreEqual(GameStateSyncType.None, recycled.StateType);
            Assert.Zero(recycled.DestroyReasons);
            Assert.IsEmpty(recycled.SyncElements);
            Assert.IsFalse(recycled.ShouldSyncFullList(list));

            states.Clear();
            Assert.IsEmpty(states.States);
            Assert.AreSame(first, states.PrepareSyncStateData(2, 43));
        }

        [Test]
        public void ServerBaseline_WritesOnlyNonEmptyStatesAndResetsThem()
        {
            var gameObject = new GameObject("baseline writer test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                var player = new LiteNetLibPlayer(manager, 1);
                var destroy = player.SyncingStates.PrepareSyncStateData(0, 42);
                destroy.StateType = GameStateSyncType.Destroy;
                destroy.DestroyReasons = 7;
                var emptyData = player.SyncingStates.PrepareSyncStateData(0, 43);
                emptyData.StateType = GameStateSyncType.Data;

                typeof(LiteNetLibGameManager).GetMethod("SyncGameStateToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.AreEqual(1, transport.Packets.Count);
                var reader = new NetDataReader(transport.Packets[0].Data);
                Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                Assert.AreEqual(manager.Tick, reader.GetPackedUInt());
                Assert.AreEqual(1, reader.GetUShort());
                Assert.AreEqual(GameStateSyncType.Destroy, (GameStateSyncType)reader.GetByte());
                Assert.AreEqual(42u, reader.GetPackedUInt());
                Assert.AreEqual(7, reader.GetByte());
                Assert.IsTrue(reader.EndOfData);
                Assert.AreEqual(GameStateSyncType.None, destroy.StateType);
                Assert.AreEqual(GameStateSyncType.None, emptyData.StateType);
                Assert.IsEmpty(player.SyncingStates.States[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ManyServerBaselineStates_AreSplitIntoCompleteOrderedPackets()
        {
            const int objectCount = 5000;
            var gameObject = new GameObject("many baseline states test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                var player = new LiteNetLibPlayer(manager, 1);
                for (uint objectId = 1; objectId <= objectCount; ++objectId)
                {
                    GameStateSyncData state = player.SyncingStates.PrepareSyncStateData(0, objectId);
                    state.StateType = GameStateSyncType.Destroy;
                    state.DestroyReasons = 7;
                }

                typeof(LiteNetLibGameManager).GetMethod("SyncGameStateToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.Greater(transport.Packets.Count, 1);
                uint nextObjectId = 1;
                int totalBytes = 0;
                int largestPacket = 0;
                foreach (RecordingGameTransport.SentPacket sent in transport.Packets)
                {
                    Assert.AreEqual(DeliveryMethod.ReliableOrdered, sent.DeliveryMethod);
                    Assert.LessOrEqual(sent.Data.Length, LiteNetLibGameManager.TARGET_BASELINE_PACKET_SIZE);
                    totalBytes += sent.Data.Length;
                    largestPacket = Math.Max(largestPacket, sent.Data.Length);
                    var reader = new NetDataReader(sent.Data);
                    Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                    Assert.AreEqual(manager.Tick, reader.GetPackedUInt());
                    ushort stateCount = reader.GetUShort();
                    Assert.Greater(stateCount, 0);
                    for (int i = 0; i < stateCount; ++i)
                    {
                        Assert.AreEqual(GameStateSyncType.Destroy, (GameStateSyncType)reader.GetByte());
                        Assert.AreEqual(nextObjectId++, reader.GetPackedUInt());
                        Assert.AreEqual(7, reader.GetByte());
                    }
                    Assert.IsTrue(reader.EndOfData);
                }
                Assert.AreEqual((uint)(objectCount + 1), nextObjectId);
                Assert.IsEmpty(player.SyncingStates.States[0]);
                TestContext.WriteLine($"{objectCount} baseline destroy states: {transport.Packets.Count} packets, {totalBytes} total bytes, {largestPacket} largest packet");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SingleOversizedServerBaselineState_RemainsIntact()
        {
            var gameObject = new GameObject("oversized baseline state test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.currentLogLevel = (ELogLevel)byte.MaxValue;
                gameObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = gameObject.AddComponent<LiteNetLibBehaviour>();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                var player = new LiteNetLibPlayer(manager, 1);
                var field = new LiteNetLibSyncField<string>
                {
                    Value = new string('x', LiteNetLibGameManager.TARGET_BASELINE_PACKET_SIZE + 100)
                };
                typeof(LiteNetLibElement).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(field, new object[] { behaviour, 123 });
                GameStateSyncData state = player.SyncingStates.PrepareSyncStateData(0, 42);
                state.StateType = GameStateSyncType.Data;
                state.SyncElements.Add(field);

                typeof(LiteNetLibGameManager).GetMethod("SyncGameStateToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.AreEqual(1, transport.Packets.Count);
                Assert.Greater(transport.Packets[0].Data.Length, LiteNetLibGameManager.TARGET_BASELINE_PACKET_SIZE);
                var reader = new NetDataReader(transport.Packets[0].Data);
                Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                reader.GetPackedUInt(); // tick
                Assert.AreEqual(1, reader.GetUShort());
                Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)reader.GetByte());
                Assert.AreEqual(42u, reader.GetPackedUInt());
                Assert.AreEqual(1, reader.GetPackedInt());
                Assert.AreEqual(123, reader.GetPackedInt());
                Assert.AreEqual(field.Value, reader.GetValue<string>());
                Assert.IsTrue(reader.EndOfData);
                Assert.IsEmpty(player.SyncingStates.States[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void OversizedDeltaElement_IsSentReliablyInstead()
        {
            var gameObject = new GameObject("oversized delta test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                gameObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = gameObject.AddComponent<LiteNetLibBehaviour>();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                var field = new LiteNetLibSyncField<string> { Value = new string('x', 1200) };
                typeof(LiteNetLibElement).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(field, new object[] { behaviour, 123 });
                var player = new LiteNetLibPlayer(manager, 1);
                player.SyncingDeltaStates.AppendDataSyncState(field);

                typeof(LiteNetLibGameManager).GetMethod("SyncDeltaDataToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.IsEmpty(transport.Packets);
                Assert.IsEmpty(player.SyncingDeltaStates.States);
                Assert.IsTrue(player.SyncingStates.States[0][0].SyncElements.Contains(field));

                typeof(LiteNetLibGameManager).GetMethod("SyncGameStateToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.AreEqual(1, transport.Packets.Count);
                Assert.AreEqual(DeliveryMethod.ReliableOrdered, transport.Packets[0].DeliveryMethod);
                var packet = new NetDataReader(transport.Packets[0].Data);
                Assert.AreEqual(GameMsgTypes.SyncBaseLine, packet.GetPackedUShort());
                packet.GetPackedUInt(); // tick
                Assert.AreEqual(1, packet.GetUShort());
                Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)packet.GetByte());
                Assert.AreEqual(0u, packet.GetPackedUInt());
                Assert.AreEqual(1, packet.GetPackedInt());
                Assert.AreEqual(123, packet.GetPackedInt());
                Assert.AreEqual(field.Value, packet.GetValue<string>());
                Assert.IsTrue(packet.EndOfData);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DeltaElementsThatFit_AreSplitIntoValidUnreliablePackets()
        {
            var gameObject = new GameObject("split delta test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                gameObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = gameObject.AddComponent<LiteNetLibBehaviour>();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                var player = new LiteNetLibPlayer(manager, 1);
                var values = new[] { new string('a', 1000), new string('b', 600), new string('c', 600) };
                for (int i = 0; i < values.Length; ++i)
                {
                    var field = new LiteNetLibSyncField<string> { Value = values[i] };
                    typeof(LiteNetLibElement).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(field, new object[] { behaviour, i + 1 });
                    player.SyncingDeltaStates.AppendDataSyncState(field);
                }

                typeof(LiteNetLibGameManager).GetMethod("SyncDeltaDataToClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { player });

                Assert.AreEqual(3, transport.Packets.Count);
                Assert.IsEmpty(player.SyncingDeltaStates.States);
                Assert.IsEmpty(player.SyncingStates.States);
                var received = new HashSet<string>();
                foreach (var sent in transport.Packets)
                {
                    Assert.AreEqual(DeliveryMethod.Unreliable, sent.DeliveryMethod);
                    Assert.LessOrEqual(sent.Data.Length, LiteNetLibGameManager.MAX_UNRELIABLE_PACKET_SIZE);
                    var packet = new NetDataReader(sent.Data);
                    Assert.AreEqual(GameMsgTypes.SyncDelta, packet.GetPackedUShort());
                    packet.GetPackedUInt(); // tick
                    Assert.AreEqual(1, packet.GetUShort());
                    Assert.AreEqual(0u, packet.GetPackedUInt());
                    Assert.Greater(packet.GetUShort(), 0); // object payload length
                    Assert.AreEqual(1, packet.GetUShort());
                    packet.GetPackedInt(); // element ID
                    received.Add(packet.GetValue<string>());
                    Assert.IsTrue(packet.EndOfData);
                }
                CollectionAssert.AreEquivalent(values, received);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BaselineReader_StopsWhenAnElementIsUnknown()
        {
            var managerObject = new GameObject("baseline read manager");
            var entityObject = new GameObject("baseline read entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.currentLogLevel = (ELogLevel)byte.MaxValue;
                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<StateReadBehaviour>();
                typeof(LiteNetLibBehaviour).GetField("_identity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(behaviour, identity);
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42));

                var writer = new NetDataWriter();
                writer.PutPackedUInt(1); // tick
                writer.Put((ushort)2); // two object states
                writer.Put((byte)GameStateSyncType.Data);
                writer.PutPackedUInt(42);
                writer.PutPackedInt(2);
                writer.PutPackedInt(int.MaxValue); // unknown element
                writer.PutPackedInt(behaviour.value.ElementId);
                writer.PutPackedInt(7);
                writer.Put((byte)GameStateSyncType.Data);
                writer.PutPackedUInt(42);
                writer.PutPackedInt(1);
                writer.PutPackedInt(behaviour.value.ElementId);
                writer.PutPackedInt(99);

                var reader = new NetDataReader(writer.CopyData());
                typeof(LiteNetLibGameManager).GetMethod("ReadGameStateFromServer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { reader });

                Assert.Zero(behaviour.value.Value);
                Assert.Greater(reader.AvailableBytes, 0, "The reader must stop at the unknown element");

                var truncated = new NetDataWriter();
                truncated.PutPackedUInt(42);
                truncated.PutPackedInt(1);
                truncated.Put((byte)251); // packed ID requires four more bytes
                var truncatedReader = new NetDataReader(truncated.CopyData());
                var readSyncState = typeof(LiteNetLibGameManager).GetMethod("ReadSyncGameState", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsFalse((bool)readSyncState.Invoke(manager, new object[] { truncatedReader, 2u }));
                Assert.Zero(behaviour.value.Value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void InitialSyncFailure_RollsBackTheSpawnedObject()
        {
            var managerObject = new GameObject("initial sync manager");
            var entityObject = new GameObject("initial sync entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.currentLogLevel = (ELogLevel)byte.MaxValue;
                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<StateReadBehaviour>();
                typeof(LiteNetLibBehaviour).GetField("_identity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(behaviour, identity);
                var writer = new NetDataWriter();
                writer.PutPackedInt(1);
                writer.PutPackedInt(int.MaxValue); // unknown initial element

                LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
                Assert.IsNull(manager.Assets.NetworkSpawn(identity, 43, -1, new NetDataReader(writer.CopyData()), 1));
                Assert.IsFalse(manager.Assets.SpawnedObjects.ContainsKey(43));
                Assert.IsFalse(identity.IsSpawned);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(LiteNetLibSyncFieldMode.ServerToOwnerClient, false)]
        [TestCase(LiteNetLibSyncFieldMode.ServerToOwnerClient, true)]
        [TestCase(LiteNetLibSyncFieldMode.ServerToClients, false)]
        [TestCase(LiteNetLibSyncFieldMode.ServerToClients, true)]
        public void ServerFieldModes_FilterInitialAndUpdateRecipients(LiteNetLibSyncFieldMode mode, bool reliableUpdate)
        {
            var managerObject = new GameObject("owner sync manager");
            var entityObject = new GameObject("owner sync entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                var owner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var observer = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                manager.AddPlayerForTest(owner);
                manager.AddPlayerForTest(observer);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerSyncBehaviour>();
                behaviour.value.syncMode = mode;
                behaviour.value.Value = 17;
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                observer.Subscribe(42);

                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });

                Assert.AreEqual(2, transport.Packets.Count, "Both subscribers must receive a spawn");
                var spawnDestinations = new HashSet<long>();
                foreach (var sent in transport.Packets)
                {
                    spawnDestinations.Add(sent.ConnectionId);
                    Assert.AreEqual(DeliveryMethod.ReliableOrdered, sent.DeliveryMethod);
                    var reader = new NetDataReader(sent.Data);
                    Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                    reader.GetPackedUInt(); // tick
                    Assert.AreEqual(1, reader.GetUShort());
                    Assert.AreEqual(GameStateSyncType.Spawn, (GameStateSyncType)reader.GetByte());
                    Assert.IsFalse(reader.GetBool()); // prefab, not scene object
                    reader.GetPackedInt(); // asset ID
                    for (int i = 0; i < 6; ++i)
                        reader.GetFloat(); // position and rotation
                    Assert.AreEqual(42u, reader.GetPackedUInt());
                    Assert.AreEqual(1L, reader.GetPackedLong());
                    int expectedElements = mode == LiteNetLibSyncFieldMode.ServerToOwnerClient && sent.ConnectionId != 1 ? 0 : 1;
                    Assert.AreEqual(expectedElements, reader.GetPackedInt());
                    if (expectedElements == 1)
                    {
                        Assert.AreEqual(behaviour.value.ElementId, reader.GetPackedInt());
                        Assert.AreEqual(17, reader.GetPackedInt());
                    }
                    Assert.IsTrue(reader.EndOfData);
                }
                CollectionAssert.AreEquivalent(new long[] { 1, 2 }, spawnDestinations);

                transport.Packets.Clear();
                manager.baseLineSyncInterval = reliableUpdate ? -1f : float.MaxValue;
                behaviour.value.Value = 29;
                syncMethod.Invoke(manager, new object[] { 2u });

                var updateDestinations = new HashSet<long>();
                foreach (var sent in transport.Packets)
                {
                    updateDestinations.Add(sent.ConnectionId);
                    var reader = new NetDataReader(sent.Data);
                    Assert.AreEqual(reliableUpdate ? GameMsgTypes.SyncBaseLine : GameMsgTypes.SyncDelta, reader.GetPackedUShort());
                    reader.GetPackedUInt(); // tick
                    Assert.AreEqual(1, reader.GetUShort()); // state or object count
                    if (reliableUpdate)
                        Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)reader.GetByte());
                    Assert.AreEqual(42u, reader.GetPackedUInt());
                    if (!reliableUpdate)
                        Assert.Greater(reader.GetUShort(), 0); // object payload length
                    Assert.AreEqual(1, reliableUpdate ? reader.GetPackedInt() : reader.GetUShort());
                    Assert.AreEqual(behaviour.value.ElementId, reader.GetPackedInt());
                    Assert.AreEqual(29, reader.GetPackedInt());
                    Assert.IsTrue(reader.EndOfData);
                }
                CollectionAssert.AreEquivalent(
                    mode == LiteNetLibSyncFieldMode.ServerToOwnerClient ? new long[] { 1 } : new long[] { 1, 2 },
                    updateDestinations);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void DoNotSyncField_StaysLocalDuringSpawnAndQueuedUpdates()
        {
            var managerObject = new GameObject("local field manager");
            var entityObject = new GameObject("local field entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                var owner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                manager.AddPlayerForTest(owner);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerSyncBehaviour>();
                behaviour.value.doNotSync = true;
                behaviour.value.Value = 17;
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));

                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });
                Assert.AreEqual(1, transport.Packets.Count);
                var reader = new NetDataReader(transport.Packets[0].Data);
                Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                reader.GetPackedUInt();
                Assert.AreEqual(1, reader.GetUShort());
                Assert.AreEqual(GameStateSyncType.Spawn, (GameStateSyncType)reader.GetByte());
                reader.GetBool();
                reader.GetPackedInt();
                for (int i = 0; i < 6; ++i)
                    reader.GetFloat();
                Assert.AreEqual(42u, reader.GetPackedUInt());
                Assert.AreEqual(1L, reader.GetPackedLong());
                Assert.AreEqual(0, reader.GetPackedInt(), "A local-only field must not appear in spawn data");
                Assert.IsTrue(reader.EndOfData);

                transport.Packets.Clear();
                behaviour.value.doNotSync = false;
                behaviour.value.Value = 29;
                manager.baseLineSyncInterval = -1f;
                syncMethod.Invoke(manager, new object[] { 2u });
                Assert.AreEqual(1, transport.Packets.Count, "Turning the flag off must allow updates again");
                reader = new NetDataReader(transport.Packets[0].Data);
                Assert.AreEqual(GameMsgTypes.SyncBaseLine, reader.GetPackedUShort());
                reader.GetPackedUInt();
                Assert.AreEqual(1, reader.GetUShort());
                Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)reader.GetByte());
                Assert.AreEqual(42u, reader.GetPackedUInt());
                Assert.AreEqual(1, reader.GetPackedInt());
                Assert.AreEqual(behaviour.value.ElementId, reader.GetPackedInt());
                Assert.AreEqual(29, reader.GetPackedInt());
                Assert.IsTrue(reader.EndOfData);

                transport.Packets.Clear();
                owner.SyncingStates.AppendDataSyncState(behaviour.value);
                owner.SyncingDeltaStates.AppendDataSyncState(behaviour.value);
                behaviour.value.doNotSync = true;
                manager.baseLineSyncInterval = float.MaxValue;
                syncMethod.Invoke(manager, new object[] { 3u });
                Assert.IsEmpty(transport.Packets, "The flag must also remove unsent baseline and delta data");
                Assert.AreEqual(29, behaviour.value.Value, "The flag must not prevent local changes");

                behaviour.value.syncMode = LiteNetLibSyncFieldMode.ClientMulticast;
                behaviour.value.doNotSync = false;
                Assert.IsTrue(behaviour.value.CanSyncFromOwnerClient());
                behaviour.value.doNotSync = true;
                Assert.IsFalse(behaviour.value.CanSyncFromOwnerClient());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnerOnlyList_FiltersSpawnAndUpdateRecipients(bool forOwnerOnly)
        {
            var managerObject = new GameObject("owner list manager");
            var entityObject = new GameObject("owner list entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                var owner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var observer = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                manager.AddPlayerForTest(owner);
                manager.AddPlayerForTest(observer);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerListBehaviour>();
                behaviour.items.forOwnerOnly = forOwnerOnly;
                behaviour.items.Add(17);
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                observer.Subscribe(42);
                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });

                Assert.AreEqual(2, transport.Packets.Count);
                foreach (var sent in transport.Packets)
                {
                    Assert.AreEqual(DeliveryMethod.ReliableOrdered, sent.DeliveryMethod);
                    if (forOwnerOnly && sent.ConnectionId == 2)
                        AssertListStatePacket(sent, behaviour.items.ElementId, true, 1, false);
                    else
                        AssertListStatePacket(sent, behaviour.items.ElementId, true, 1, false, 17);
                }

                transport.Packets.Clear();
                behaviour.items.Add(29);
                syncMethod.Invoke(manager, new object[] { 2u });
                Assert.AreEqual(forOwnerOnly ? 1 : 2, transport.Packets.Count);
                foreach (var sent in transport.Packets)
                {
                    if (forOwnerOnly)
                        Assert.AreEqual(1, sent.ConnectionId);
                    AssertListStatePacket(sent, behaviour.items.ElementId, false, 1, false, 29);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void OwnerOnlyList_FiltersAnUpdateQueuedBeforeFlagChanged()
        {
            var managerObject = new GameObject("queued owner list manager");
            var entityObject = new GameObject("queued owner list entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                var owner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var observer = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                manager.AddPlayerForTest(owner);
                manager.AddPlayerForTest(observer);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerListBehaviour>();
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                observer.Subscribe(42);
                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });
                transport.Packets.Clear();

                behaviour.items.Add(29);
                owner.SyncingStates.AppendDataSyncState(behaviour.items);
                observer.SyncingStates.AppendDataSyncState(behaviour.items);
                behaviour.items.forOwnerOnly = true;
                syncMethod.Invoke(manager, new object[] { 2u });

                Assert.AreEqual(1, transport.Packets.Count);
                Assert.AreEqual(1, transport.Packets[0].ConnectionId);
                AssertListStatePacket(transport.Packets[0], behaviour.items.ElementId, false, 1, false, 29);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnerOnlyList_TransfersCurrentContentsToNewOwner(bool newOwnerSpawnPending)
        {
            var managerObject = new GameObject("transfer owner list manager");
            var entityObject = new GameObject("transfer owner list entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                manager.ActivateServerForTest();
                var previousOwner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var newOwner = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                manager.AddPlayerForTest(previousOwner);
                manager.AddPlayerForTest(newOwner);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerListBehaviour>();
                behaviour.items.forOwnerOnly = true;
                behaviour.items.Add(17);
                behaviour.items.Add(29);
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                newOwner.Subscribe(42);
                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!newOwnerSpawnPending)
                    syncMethod.Invoke(manager, new object[] { 1u });
                transport.Packets.Clear();

                manager.Assets.SetObjectOwnerImmediately(42, 2);
                syncMethod.Invoke(manager, new object[] { 2u });

                int newOwnerStatePackets = 0;
                foreach (var sent in transport.Packets)
                {
                    var reader = new NetDataReader(sent.Data);
                    if (reader.GetPackedUShort() != GameMsgTypes.SyncBaseLine)
                        continue;
                    if (sent.ConnectionId == 2)
                    {
                        ++newOwnerStatePackets;
                        AssertListStatePacket(sent, behaviour.items.ElementId, newOwnerSpawnPending, 2,
                            !newOwnerSpawnPending, 17, 29);
                    }
                    else
                    {
                        Assert.AreEqual(1, sent.ConnectionId);
                        AssertListStatePacket(sent, behaviour.items.ElementId, true, 2, false);
                    }
                }
                Assert.AreEqual(1, newOwnerStatePackets);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void OwnerOnlyList_RapidTransfersDiscardIntermediateFullState()
        {
            var managerObject = new GameObject("rapid owner list manager");
            var entityObject = new GameObject("rapid owner list entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                manager.ActivateServerForTest();
                var firstOwner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var secondOwner = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                var thirdOwner = new LiteNetLibPlayer(manager, 3) { IsReady = true };
                manager.AddPlayerForTest(firstOwner);
                manager.AddPlayerForTest(secondOwner);
                manager.AddPlayerForTest(thirdOwner);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerListBehaviour>();
                behaviour.items.forOwnerOnly = true;
                behaviour.items.Add(17);
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                secondOwner.Subscribe(42);
                thirdOwner.Subscribe(42);
                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });
                transport.Packets.Clear();

                firstOwner.SyncingStates.AppendDataSyncState(behaviour.items);
                manager.Assets.SetObjectOwnerImmediately(42, 2);
                manager.Assets.SetObjectOwnerImmediately(42, 3);
                syncMethod.Invoke(manager, new object[] { 2u });

                int statePackets = 0;
                foreach (var sent in transport.Packets)
                {
                    var reader = new NetDataReader(sent.Data);
                    if (reader.GetPackedUShort() != GameMsgTypes.SyncBaseLine)
                        continue;
                    ++statePackets;
                    Assert.AreEqual(3, sent.ConnectionId);
                    AssertListStatePacket(sent, behaviour.items.ElementId, false, 3, true, 17);
                }
                Assert.AreEqual(1, statePackets);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnerTransfer_SendsCurrentOwnerOnlyValueToNewOwner(bool newOwnerSpawnPending)
        {
            var managerObject = new GameObject("owner transfer manager");
            var entityObject = new GameObject("owner transfer entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                manager.ActivateServerForTest();
                var previousOwner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var newOwner = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                manager.AddPlayerForTest(previousOwner);
                manager.AddPlayerForTest(newOwner);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerSyncBehaviour>();
                behaviour.value.syncMode = LiteNetLibSyncFieldMode.ServerToOwnerClient;
                behaviour.value.Value = 17;
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                newOwner.Subscribe(42);

                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!newOwnerSpawnPending)
                    syncMethod.Invoke(manager, new object[] { 1u });
                transport.Packets.Clear();

                manager.Assets.SetObjectOwnerImmediately(42, 2);
                Assert.AreEqual(2, identity.ConnectionId);
                syncMethod.Invoke(manager, new object[] { 2u });

                int newOwnerStatePackets = 0;
                foreach (var sent in transport.Packets)
                {
                    var reader = new NetDataReader(sent.Data);
                    if (reader.GetPackedUShort() != GameMsgTypes.SyncBaseLine || sent.ConnectionId != 2)
                        continue;
                    ++newOwnerStatePackets;
                    Assert.AreEqual(DeliveryMethod.ReliableOrdered, sent.DeliveryMethod);
                    reader.GetPackedUInt(); // tick
                    Assert.AreEqual(1, reader.GetUShort());
                    Assert.AreEqual(newOwnerSpawnPending ? GameStateSyncType.Spawn : GameStateSyncType.Data,
                        (GameStateSyncType)reader.GetByte());
                    if (newOwnerSpawnPending)
                    {
                        Assert.IsFalse(reader.GetBool());
                        reader.GetPackedInt(); // asset ID
                        for (int i = 0; i < 6; ++i)
                            reader.GetFloat(); // position and rotation
                    }
                    Assert.AreEqual(42u, reader.GetPackedUInt());
                    if (newOwnerSpawnPending)
                        Assert.AreEqual(2L, reader.GetPackedLong());
                    Assert.AreEqual(1, reader.GetPackedInt());
                    Assert.AreEqual(behaviour.value.ElementId, reader.GetPackedInt());
                    Assert.AreEqual(17, reader.GetPackedInt());
                    Assert.IsTrue(reader.EndOfData);
                }
                Assert.AreEqual(1, newOwnerStatePackets);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void RapidOwnerTransfers_DiscardQueuedOwnerOnlyUpdatesForPreviousOwners()
        {
            var managerObject = new GameObject("rapid owner transfer manager");
            var entityObject = new GameObject("rapid owner transfer entity");
            try
            {
                var manager = managerObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                var transport = new RecordingGameTransport();
                manager.UseServerTransportForTest(transport);
                manager.EnableServerForTest();
                manager.ActivateServerForTest();
                var firstOwner = new LiteNetLibPlayer(manager, 1) { IsReady = true };
                var secondOwner = new LiteNetLibPlayer(manager, 2) { IsReady = true };
                var thirdOwner = new LiteNetLibPlayer(manager, 3) { IsReady = true };
                manager.AddPlayerForTest(firstOwner);
                manager.AddPlayerForTest(secondOwner);
                manager.AddPlayerForTest(thirdOwner);

                var identity = entityObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = entityObject.AddComponent<OwnerSyncBehaviour>();
                behaviour.value.syncMode = LiteNetLibSyncFieldMode.ServerToOwnerClient;
                behaviour.value.Value = 17;
                Assert.AreSame(identity, manager.Assets.NetworkSpawn(identity, 42, 1));
                secondOwner.Subscribe(42);
                thirdOwner.Subscribe(42);
                var syncMethod = typeof(LiteNetLibGameManager).GetMethod("ProceedServerGameStateSync", BindingFlags.Instance | BindingFlags.NonPublic);
                syncMethod.Invoke(manager, new object[] { 1u });
                transport.Packets.Clear();

                firstOwner.SyncingStates.AppendDataSyncState(behaviour.value);
                firstOwner.SyncingDeltaStates.AppendDataSyncState(behaviour.value);
                manager.Assets.SetObjectOwnerImmediately(42, 2);
                manager.Assets.SetObjectOwnerImmediately(42, 3);
                syncMethod.Invoke(manager, new object[] { 2u });

                int ownerOnlyStatePackets = 0;
                foreach (var sent in transport.Packets)
                {
                    var reader = new NetDataReader(sent.Data);
                    ushort messageType = reader.GetPackedUShort();
                    if (messageType != GameMsgTypes.SyncBaseLine && messageType != GameMsgTypes.SyncDelta)
                        continue;
                    Assert.AreEqual(3, sent.ConnectionId, "Only the final owner should receive queued owner-only state");
                    Assert.AreEqual(GameMsgTypes.SyncBaseLine, messageType);
                    ++ownerOnlyStatePackets;
                    reader.GetPackedUInt(); // tick
                    Assert.AreEqual(1, reader.GetUShort());
                    Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)reader.GetByte());
                    Assert.AreEqual(42u, reader.GetPackedUInt());
                    Assert.AreEqual(1, reader.GetPackedInt());
                    Assert.AreEqual(behaviour.value.ElementId, reader.GetPackedInt());
                    Assert.AreEqual(17, reader.GetPackedInt());
                    Assert.IsTrue(reader.EndOfData);
                }
                Assert.AreEqual(1, ownerOnlyStatePackets);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase(LayoutDifference.ExtraClientField, true)]
        [TestCase(LayoutDifference.ExtraServerField, false)]
        [TestCase(LayoutDifference.LeadingServerBehaviour, false)]
        [TestCase(LayoutDifference.TrailingServerBehaviour, true)]
        public void DifferentBehaviourAndFieldLayouts_HavePredictableInitialSyncResults(LayoutDifference difference, bool shouldSpawn)
        {
            var serverManagerObject = new GameObject("layout server manager");
            var clientManagerObject = new GameObject("layout client manager");
            var serverObject = new GameObject("layout server entity");
            var clientObject = new GameObject("layout client entity");
            try
            {
                var serverManager = serverManagerObject.AddComponent<GameManagerHarness>();
                serverManager.InitializeForTest();
                serverManager.currentLogLevel = (ELogLevel)byte.MaxValue;
                var clientManager = clientManagerObject.AddComponent<GameManagerHarness>();
                clientManager.InitializeForTest();
                clientManager.currentLogLevel = (ELogLevel)byte.MaxValue;

                var serverIdentity = serverObject.AddComponent<LiteNetLibIdentity>();
                if (difference == LayoutDifference.LeadingServerBehaviour)
                    serverObject.AddComponent<LiteNetLibBehaviour>();
                var serverBehaviour = serverObject.AddComponent<LayoutSyncBehaviour>();
                if (difference == LayoutDifference.TrailingServerBehaviour)
                    serverObject.AddComponent<LiteNetLibBehaviour>();
                if (difference != LayoutDifference.ExtraServerField)
                    serverBehaviour.optional = null;
                serverBehaviour.shared.Value = 77;
                if (serverBehaviour.optional != null)
                    serverBehaviour.optional.Value = 88;

                var clientIdentity = clientObject.AddComponent<LiteNetLibIdentity>();
                var clientBehaviour = clientObject.AddComponent<LayoutSyncBehaviour>();
                if (difference != LayoutDifference.ExtraClientField)
                    clientBehaviour.optional = null;
                foreach (var behaviour in serverObject.GetComponents<LiteNetLibBehaviour>())
                    BindBehaviourIdentity(behaviour, serverIdentity);
                BindBehaviourIdentity(clientBehaviour, clientIdentity);

                Assert.AreSame(serverIdentity, serverManager.Assets.NetworkSpawn(serverIdentity, 42));
                var elements = new HashSet<LiteNetLibSyncElement> { serverBehaviour.shared };
                if (serverBehaviour.optional != null)
                    elements.Add(serverBehaviour.optional);
                var writer = new NetDataWriter();
                typeof(LiteNetLibGameManager).GetMethod("WriteSyncElements", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(serverManager, new object[] { writer, elements, 1u, true });

                if (!shouldSpawn)
                    LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
                var reader = new NetDataReader(writer.CopyData());
                var spawned = clientManager.Assets.NetworkSpawn(clientIdentity, 42, -1, reader, 1);
                if (difference == LayoutDifference.LeadingServerBehaviour)
                    Assert.AreNotEqual(serverBehaviour.shared.ElementId, clientBehaviour.shared.ElementId);
                else if (difference == LayoutDifference.TrailingServerBehaviour)
                    Assert.AreEqual(serverBehaviour.shared.ElementId, clientBehaviour.shared.ElementId);
                if (shouldSpawn)
                {
                    Assert.AreSame(clientIdentity, spawned);
                    Assert.AreEqual(77, clientBehaviour.shared.Value);
                    Assert.IsTrue(reader.EndOfData);
                }
                else
                {
                    Assert.IsNull(spawned);
                    Assert.IsFalse(clientManager.Assets.SpawnedObjects.ContainsKey(42));
                    Assert.IsFalse(clientIdentity.IsSpawned);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clientObject);
                UnityEngine.Object.DestroyImmediate(serverObject);
                UnityEngine.Object.DestroyImmediate(clientManagerObject);
                UnityEngine.Object.DestroyImmediate(serverManagerObject);
            }
        }

        private static void BindBehaviourIdentity(LiteNetLibBehaviour behaviour, LiteNetLibIdentity identity)
        {
            typeof(LiteNetLibBehaviour).GetField("_identity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(behaviour, identity);
        }

        [Test]
        public void DeltaReader_RejectsAnInvalidLengthWithoutMovingPastThePacket()
        {
            var gameObject = new GameObject("delta reader test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.currentLogLevel = (ELogLevel)byte.MaxValue;
                var method = typeof(LiteNetLibGameManager).GetMethod("ReadDeltaDataFromServer", BindingFlags.Instance | BindingFlags.NonPublic);

                var invalid = new NetDataWriter();
                invalid.PutPackedUInt(1);
                invalid.Put((ushort)1);
                invalid.PutPackedUInt(42);
                invalid.Put((ushort)100);
                var invalidReader = new NetDataReader(invalid.CopyData());
                Assert.DoesNotThrow(() => method.Invoke(manager, new object[] { invalidReader }));
                Assert.AreEqual(invalid.Length, invalidReader.Position);

                var unknownObjects = new NetDataWriter();
                unknownObjects.PutPackedUInt(1);
                unknownObjects.Put((ushort)2);
                for (uint objectId = 42; objectId <= 43; ++objectId)
                {
                    unknownObjects.PutPackedUInt(objectId);
                    unknownObjects.Put((ushort)2);
                    unknownObjects.Put((ushort)0);
                }
                var validReader = new NetDataReader(unknownObjects.CopyData());
                Assert.DoesNotThrow(() => method.Invoke(manager, new object[] { validReader }));
                Assert.IsTrue(validReader.EndOfData);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PendingRpcs_AreCopiedAndBoundedByCountAndBytes()
        {
            var gameObject = new GameObject("pending RPC queue test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.currentLogLevel = (ELogLevel)byte.MaxValue;
                manager.loadOfflineSceneWhenClientStopped = false;
                byte[] payload = { 7 };
                for (uint objectId = 1; objectId <= manager.PendingRpcCountLimit + 1; ++objectId)
                    manager.QueueRpcForTest(objectId, payload, 10);

                payload[0] = 9;
                Assert.AreEqual(manager.PendingRpcCountLimit, manager.PendingRpcCount);
                Assert.AreEqual(manager.PendingRpcCountLimit, manager.PendingRpcBytes);
                Assert.AreEqual(2u, manager.OldestPendingRpcObjectId);
                Assert.AreEqual(7, manager.OldestPendingRpcFirstByte);

                manager.OnStopClient();
                Assert.Zero(manager.PendingRpcCount);
                Assert.Zero(manager.PendingRpcBytes);

                byte[] largePayload = new byte[manager.PendingRpcByteLimit / 4];
                for (uint objectId = 1; objectId <= 5; ++objectId)
                    manager.QueueRpcForTest(objectId, largePayload, 20);

                Assert.AreEqual(4, manager.PendingRpcCount);
                Assert.AreEqual(manager.PendingRpcByteLimit, manager.PendingRpcBytes);
                Assert.AreEqual(2u, manager.OldestPendingRpcObjectId);

                manager.QueueRpcForTest(6, new byte[manager.PendingRpcByteLimit + 1], 20);
                Assert.AreEqual(4, manager.PendingRpcCount);
                Assert.AreEqual(manager.PendingRpcByteLimit, manager.PendingRpcBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PendingRpcs_ExpireWithoutAnObjectSpawn()
        {
            var gameObject = new GameObject("pending RPC expiration test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                manager.QueueRpcForTest(1, new byte[3], 100);
                manager.QueueRpcForTest(2, new byte[5], 101);

                manager.PruneRpcQueueForTest(100 + manager.PendingRpcLifetime);
                Assert.AreEqual(1, manager.PendingRpcCount);
                Assert.AreEqual(5, manager.PendingRpcBytes);
                Assert.AreEqual(2u, manager.OldestPendingRpcObjectId);

                manager.PruneRpcQueueForTest(101 + manager.PendingRpcLifetime);
                Assert.Zero(manager.PendingRpcCount);
                Assert.Zero(manager.PendingRpcBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GenericSyncField_NullToNullIsNotAChange()
        {
            var field = new ComparableStringField();
            Assert.IsFalse(field.HasChanged(null, null));
            Assert.IsTrue(field.HasChanged(null, "value"));
            Assert.IsTrue(field.HasChanged("value", null));
            Assert.IsFalse(field.HasChanged("value", "value"));
        }

        [Test]
        public void ManagerDataStateAndGenericField_DoNotAllocateAfterWarmup()
        {
            var gameObject = new GameObject("data writer test");
            try
            {
                var manager = gameObject.AddComponent<GameManagerHarness>();
                manager.InitializeForTest();
                gameObject.AddComponent<LiteNetLibIdentity>();
                var behaviour = gameObject.AddComponent<LiteNetLibBehaviour>();
                var field = new LiteNetLibSyncField<int> { Value = 123 };
                typeof(LiteNetLibElement).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(field, new object[] { behaviour, 123 });
                var state = new GameStateSyncData();
                var player = new LiteNetLibPlayer(manager, 1);
                var writer = new NetDataWriter(true, 1024);
                var method = typeof(LiteNetLibGameManager).GetMethod("WriteServerGameState", BindingFlags.Instance | BindingFlags.NonPublic);
                var write = (WriteServerState)Delegate.CreateDelegate(typeof(WriteServerState), manager, method);

                long allocations = AllocationCount(() =>
                {
                    writer.Reset();
                    writer.PutPackedUInt(manager.Tick);
                    writer.Put((ushort)1);
                    state.StateType = GameStateSyncType.Data;
                    state.SyncElements.Add(field);
                    write(writer, player, 42, state, manager.Tick);
                    state.Reset();
                });
                Assert.Zero(allocations, $"Manager data state allocated {allocations} times in {Iterations} updates");

                var packet = new NetDataReader(writer.CopyData());
                Assert.AreEqual(manager.Tick, packet.GetPackedUInt());
                Assert.AreEqual(1, packet.GetUShort());
                Assert.AreEqual(GameStateSyncType.Data, (GameStateSyncType)packet.GetByte());
                Assert.AreEqual(42u, packet.GetPackedUInt());
                Assert.AreEqual(1, packet.GetPackedInt());
                Assert.AreEqual(123, packet.GetPackedInt());
                Assert.AreEqual(123, packet.GetPackedInt());
                Assert.IsTrue(packet.EndOfData);

                var valueWriter = new NetDataWriter();
                valueWriter.PutValue(321);
                var valueReader = new NetDataReader(valueWriter.CopyData());
                var deserializeMethod = typeof(LiteNetLibSyncField<int>).GetMethod("DeserializeValue", BindingFlags.Instance | BindingFlags.NonPublic);
                var deserialize = (Action<NetDataReader>)Delegate.CreateDelegate(typeof(Action<NetDataReader>), field, deserializeMethod);
                long readAllocations = AllocationCount(() =>
                {
                    valueReader.SetPosition(0);
                    deserialize(valueReader);
                });
                Assert.Zero(readAllocations, $"Generic sync field reads allocated {readAllocations} times in {Iterations} updates");
                Assert.AreEqual(321, field.Value);

                int nextValue = 0;
                long changeAllocations = AllocationCount(() => field.Value = ++nextValue);
                Assert.Zero(changeAllocations, $"Generic sync field changes allocated {changeAllocations} times in {Iterations} updates");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    public class GameManagerHarness : LiteNetLibGameManager
    {
        public void EnableServerForTest()
        {
            typeof(LiteNetLibManager).GetProperty("IsServer").SetValue(this, true);
            typeof(LiteNetLibManager).GetField("_serverTransport", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, Server.Transport);
        }

        public void ActivateServerForTest()
        {
            typeof(LiteNetLibServer).GetField("_isNetworkActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Server, true);
        }

        public void AddPlayerForTest(LiteNetLibPlayer player)
        {
            Players.Add(player.ConnectionId, player);
        }

        public void UseServerTransportForTest(ITransport transport)
        {
            Server = new LiteNetLibServer(this) { Transport = transport };
        }

        public int PendingRpcCountLimit => MaxPendingRpcCount;
        public int PendingRpcByteLimit => MaxPendingRpcBytes;
        public double PendingRpcLifetime => PendingRpcLifetimeSeconds;
        public int PendingRpcCount => _pendingRpcs.Count;
        public int PendingRpcBytes => _pendingRpcBytes;
        public uint OldestPendingRpcObjectId => _pendingRpcs[0].info.objectId;
        public byte OldestPendingRpcFirstByte => _pendingRpcs[0].reader.RawData[0];

        public void QueueRpcForTest(uint objectId, byte[] payload, double now)
        {
            QueuePendingRpc(new LiteNetLibElementInfo { objectId = objectId }, new NetDataReader(payload), now);
        }

        public void PruneRpcQueueForTest(double now)
        {
            PruneExpiredPendingRpcs(now);
        }

        public void InitializeForTest()
        {
            _logicUpdater = new LogicUpdater();
            Assets = GetComponent<LiteNetLibAssets>();
            typeof(LiteNetLibAssets).GetProperty("Manager").SetValue(Assets, this);
            InterestManager = GetComponent<DefaultInterestManager>() ?? gameObject.AddComponent<DefaultInterestManager>();
        }
        protected override void OnDestroy() { }
    }

    public class ComparableStringField : LiteNetLibSyncField<string>
    {
        public bool HasChanged(string oldValue, string newValue) => IsValueChanged(oldValue, newValue);
    }

    public class StateReadBehaviour : LiteNetLibBehaviour
    {
        public LiteNetLibSyncField<int> value = new LiteNetLibSyncField<int>();
    }

    public class LayoutSyncBehaviour : LiteNetLibBehaviour
    {
        public LiteNetLibSyncField<int> shared = new LiteNetLibSyncField<int>();
        public LiteNetLibSyncField<int> optional = new LiteNetLibSyncField<int>();
    }

    public class OwnerSyncBehaviour : LiteNetLibBehaviour
    {
        public SyncFieldInt value = new SyncFieldInt();
    }

    public class OwnerListBehaviour : LiteNetLibBehaviour
    {
        public SyncListInt items = new SyncListInt();
    }

    public class RecordingGameTransport : ITransport
    {
        public struct SentPacket
        {
            public long ConnectionId;
            public DeliveryMethod DeliveryMethod;
            public byte[] Data;
        }

        public readonly List<SentPacket> Packets = new List<SentPacket>();
        public int ServerPeersCount => 0;
        public int ServerMaxConnections => 0;
        public bool IsClientStarted => false;
        public bool IsServerStarted => true;
        public bool HasImplementedPing => false;
        public bool IsReliableOnly => false;
        public bool StartClient(string address, int port) => false;
        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer) => false;
        public bool ClientReceive(out TransportEventData eventData) { eventData = default; return false; }
        public void StopClient() { }
        public bool StartServer(int port, int maxConnections) => false;
        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Packets.Add(new SentPacket { ConnectionId = connectionId, DeliveryMethod = deliveryMethod, Data = writer.CopyData() });
            return true;
        }
        public bool ServerReceive(out TransportEventData eventData) { eventData = default; return false; }
        public bool ServerDisconnect(long connectionId) => false;
        public void StopServer() { }
        public void Destroy() { }
        public long GetClientRtt() => 0;
        public long GetServerRtt(long connectionId) => 0;
    }
}
