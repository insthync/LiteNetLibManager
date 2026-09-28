using System;
using System.Collections.Generic;
using System.Reflection;
using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace LiteNetLibManager.Tests
{
    public class GameManagerStateSyncTests
    {
        private const int Iterations = 256;
        private delegate ushort WriteServerState(NetDataWriter writer, LiteNetLibPlayer player, Dictionary<uint, GameStateSyncData> states);

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
            states.ClearChannel(1);

            Assert.IsEmpty(states.States[1]);
            var recycled = states.PrepareSyncStateData(1, 42);
            Assert.AreSame(first, recycled);
            Assert.AreEqual(GameStateSyncType.None, recycled.StateType);
            Assert.Zero(recycled.DestroyReasons);
            Assert.IsEmpty(recycled.SyncElements);

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
                var player = new LiteNetLibPlayer(manager, 1);
                var destroy = new GameStateSyncData { StateType = GameStateSyncType.Destroy, DestroyReasons = 7 };
                var emptyData = new GameStateSyncData { StateType = GameStateSyncType.Data };
                var states = new Dictionary<uint, GameStateSyncData> { [42] = destroy, [43] = emptyData };
                var writer = new NetDataWriter();
                var method = typeof(LiteNetLibGameManager).GetMethod("WriteGameStateFromServer", BindingFlags.Instance | BindingFlags.NonPublic);

                ushort count = (ushort)method.Invoke(manager, new object[] { writer, player, states });

                Assert.AreEqual(1, count);
                var reader = new NetDataReader(writer.CopyData());
                Assert.AreEqual(manager.Tick, reader.GetPackedUInt());
                Assert.AreEqual(1, reader.GetUShort());
                Assert.AreEqual(GameStateSyncType.Destroy, (GameStateSyncType)reader.GetByte());
                Assert.AreEqual(42u, reader.GetPackedUInt());
                Assert.AreEqual(7, reader.GetByte());
                Assert.IsTrue(reader.EndOfData);
                Assert.AreEqual(GameStateSyncType.None, destroy.StateType);
                Assert.AreEqual(GameStateSyncType.None, emptyData.StateType);
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
                var states = new Dictionary<uint, GameStateSyncData> { [42] = state };
                var player = new LiteNetLibPlayer(manager, 1);
                var writer = new NetDataWriter(true, 1024);
                var method = typeof(LiteNetLibGameManager).GetMethod("WriteGameStateFromServer", BindingFlags.Instance | BindingFlags.NonPublic);
                var write = (WriteServerState)Delegate.CreateDelegate(typeof(WriteServerState), manager, method);

                long allocations = AllocationCount(() =>
                {
                    writer.Reset();
                    state.StateType = GameStateSyncType.Data;
                    state.SyncElements.Add(field);
                    write(writer, player, states);
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
        }
        protected override void OnDestroy() { }
    }

    public class ComparableStringField : LiteNetLibSyncField<string>
    {
        public bool HasChanged(string oldValue, string newValue) => IsValueChanged(oldValue, newValue);
    }

    public class RecordingGameTransport : ITransport
    {
        public struct SentPacket
        {
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
            Packets.Add(new SentPacket { DeliveryMethod = deliveryMethod, Data = writer.CopyData() });
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
