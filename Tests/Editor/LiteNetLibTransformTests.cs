using System.Collections.Generic;
using System.Reflection;
using LiteNetLib.Utils;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace LiteNetLibManager.Tests
{
    public class LiteNetLibTransformTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private delegate void StoreSyncBufferDelegate(LiteNetLibTransform networkTransform,
            SortedList<uint, LiteNetLibTransform.TransformData> buffers,
            LiteNetLibTransform.TransformData data, int maxBuffers);

        [Test]
        public void SingleReceivedSample_ReachesItsFinalPose()
        {
            var gameObject = new GameObject("single transform sample");
            try
            {
                var networkTransform = gameObject.AddComponent<LiteNetLibTransform>();
                networkTransform.interpolationTicks = 2;
                var buffers = (SortedList<uint, LiteNetLibTransform.TransformData>)
                    typeof(LiteNetLibTransform).GetField("_interpBuffers", PrivateInstance).GetValue(networkTransform);
                buffers.Add(10, new LiteNetLibTransform.TransformData
                {
                    Tick = 10,
                    SyncData = LiteNetLibTransform.SyncTransformState.PositionX,
                    Position = new Vector3(7f, 0f, 0f),
                });
                typeof(LiteNetLibTransform).GetField("_interpTick", PrivateInstance).SetValue(networkTransform, 11u);

                var interpolate = typeof(LiteNetLibTransform).GetMethod("InterpolateTransform", PrivateInstance);
                interpolate.Invoke(networkTransform, null);
                Assert.AreEqual(0f, gameObject.transform.position.x, "The sample should wait for the render tick");

                typeof(LiteNetLibTransform).GetField("_interpTick", PrivateInstance).SetValue(networkTransform, 12u);
                interpolate.Invoke(networkTransform, null);
                Assert.AreEqual(7f, gameObject.transform.position.x);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RotationDetection_TracksConfiguredEulerAxes()
        {
            var gameObject = new GameObject("transform rotation detection");
            try
            {
                var networkTransform = gameObject.AddComponent<LiteNetLibTransform>();
                networkTransform.eulerAnglesThreshold = 1f;
                var hasRotationChanged = typeof(LiteNetLibTransform).GetMethod("HasRotationChanged", PrivateInstance);

                networkTransform.syncData = LiteNetLibTransform.SyncTransformState.EulerAnglesZ;
                Assert.IsTrue((bool)hasRotationChanged.Invoke(networkTransform,
                    new object[] { new Vector3(0f, 0f, 90f), Vector3.zero }));
                Assert.IsFalse((bool)hasRotationChanged.Invoke(networkTransform,
                    new object[] { new Vector3(0f, 0f, 0.5f), Vector3.zero }));

                networkTransform.syncData = LiteNetLibTransform.SyncTransformState.EulerAnglesY;
                Assert.IsFalse((bool)hasRotationChanged.Invoke(networkTransform,
                    new object[] { new Vector3(0f, 0f, 90f), Vector3.zero }));
                Assert.IsTrue((bool)hasRotationChanged.Invoke(networkTransform,
                    new object[] { new Vector3(0f, 90f, 0f), Vector3.zero }));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void OlderTransformState_DoesNotReplaceTheAcceptedBuffer()
        {
            var field = new LiteNetLibTransform.SyncTransformsField();
            var readSyncData = typeof(LiteNetLibSyncField<LiteNetLibTransform.SyncTransforms>)
                .GetMethod("ReadSyncData", PrivateInstance);
            var writer = new NetDataWriter();

            var current = new LiteNetLibTransform.SyncTransforms();
            current.Add(10, new LiteNetLibTransform.TransformData
            {
                Tick = 10,
                SyncData = LiteNetLibTransform.SyncTransformState.PositionX,
                Position = new Vector3(10f, 0f, 0f),
            });
            current.Serialize(writer);
            readSyncData.Invoke(field, new object[] { 10u, false, new NetDataReader(writer.CopyData()) });
            Assert.AreEqual(10f, field.Value[10].Position.x);

            writer.Reset();
            var older = new LiteNetLibTransform.SyncTransforms();
            older.Add(9, new LiteNetLibTransform.TransformData
            {
                Tick = 9,
                SyncData = LiteNetLibTransform.SyncTransformState.PositionX,
                Position = new Vector3(9f, 0f, 0f),
            });
            older.Serialize(writer);
            readSyncData.Invoke(field, new object[] { 9u, false, new NetDataReader(writer.CopyData()) });

            Assert.AreEqual(1, field.Value.Count);
            Assert.IsTrue(field.Value.ContainsKey(10));
            Assert.AreEqual(10f, field.Value[10].Position.x);
        }

        [Test]
        public void ExtraData_RejectsLengthsThatDoNotFitOnTheWire()
        {
            var writer = new NetDataWriter();
            var data = new LiteNetLibTransform.TransformData { Extra = new byte[256] };

            Assert.Throws<System.ArgumentOutOfRangeException>(() => data.Serialize(writer));
            Assert.Zero(writer.Length, "An invalid sample must not leave a partial packet");

            data.Extra = new byte[byte.MaxValue];
            data.Serialize(writer);
            var received = new LiteNetLibTransform.TransformData();
            received.Deserialize(new NetDataReader(writer.CopyData()));
            Assert.AreEqual(byte.MaxValue, received.Extra.Length);
        }

        [Test]
        public void OutgoingExtraBuffers_AreReusedWithoutSteadyAllocations()
        {
            var gameObject = new GameObject("transform extra buffers");
            try
            {
                var networkTransform = gameObject.AddComponent<LiteNetLibTransform>();
                networkTransform.onWriteSyncBuffer += (writer, tick) => writer.Put((byte)tick);
                var buffers = new SortedList<uint, LiteNetLibTransform.TransformData>();
                var method = typeof(LiteNetLibTransform).GetMethod("StoreSyncBuffer", PrivateInstance);
                var store = (StoreSyncBufferDelegate)System.Delegate.CreateDelegate(
                    typeof(StoreSyncBufferDelegate), method);

                for (uint tick = 1; tick <= 3; ++tick)
                    store(networkTransform, buffers, new LiteNetLibTransform.TransformData { Tick = tick }, 3);
                byte[] recycled = buffers[1].Extra;
                store(networkTransform, buffers, new LiteNetLibTransform.TransformData { Tick = 4 }, 3);
                store(networkTransform, buffers, new LiteNetLibTransform.TransformData { Tick = 5 }, 3);
                Assert.AreSame(recycled, buffers[5].Extra);
                for (uint tick = 3; tick <= 5; ++tick)
                    Assert.AreEqual((byte)tick, buffers[tick].Extra[0]);

                for (uint tick = 6; tick < 64; ++tick)
                    store(networkTransform, buffers, new LiteNetLibTransform.TransformData { Tick = tick }, 3);
                using (var recorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal, "GC.Alloc", 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    for (uint tick = 64; tick < 320; ++tick)
                        store(networkTransform, buffers, new LiteNetLibTransform.TransformData { Tick = tick }, 3);
                    recorder.Stop();
                    long allocations = recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
                    Assert.Zero(allocations, $"Outgoing transform extras allocated {allocations} times");
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
