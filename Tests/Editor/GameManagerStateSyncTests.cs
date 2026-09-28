using System;
using System.Collections.Generic;
using System.Reflection;
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
}
