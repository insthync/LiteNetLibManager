using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace LiteNetLibManager.Tests
{
    public class GcAllocationTests
    {
        private const int Iterations = 256;

        private static (long Count, long Bytes) Allocations(Action action)
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
                if (recorder.Count == 0)
                    return (0, 0);
                var sample = recorder.GetSample(0);
                return (sample.Count, sample.Value);
            }
        }

        [Test]
        public void BuiltInSerializationAndCollectionWrites_DoNotAllocateAfterWarmup()
        {
            var writer = new NetDataWriter(true, 256);
            var reader = new NetDataReader(new byte[] { 2 });
            var vectorWriter = new NetDataWriter();
            vectorWriter.PutVector3(new Vector3(1, 2, 3));
            var vectorReader = new NetDataReader(vectorWriter.CopyData());
            IList<string> list = new List<string> { "a", "b", "c" };
            IList<int> integers = new List<int> { 1, 2, 3 };
            IDictionary<string, string> dictionary = new Dictionary<string, string> { ["a"] = "b" };
            IDictionary<int, int> integerDictionary = new Dictionary<int, int> { [1] = 2 };
            int sink = 0;
            object allocated = null;

            var positiveControl = Allocations(() => { allocated = new byte[16]; });
            var control = Allocations(() => { writer.Reset(); writer.PutPackedInt(123); });
            var integerWrite = Allocations(() => { writer.Reset(); writer.PutValue(123); });
            var integerRead = Allocations(() => { reader.SetPosition(0); sink += reader.GetValue<int>(); });
            var vectorWrite = Allocations(() => { writer.Reset(); writer.PutValue(new Vector3(1, 2, 3)); });
            var vectorRead = Allocations(() => { vectorReader.SetPosition(0); sink += (int)vectorReader.GetValue<Vector3>().x; });
            var listWrite = Allocations(() => { writer.Reset(); writer.PutList(list); });
            var integerListWrite = Allocations(() => { writer.Reset(); writer.PutList(integers); });
            var dictionaryWrite = Allocations(() => { writer.Reset(); writer.PutDictionary(dictionary); });
            var integerDictionaryWrite = Allocations(() => { writer.Reset(); writer.PutDictionary(integerDictionary); });

            Assert.Greater(positiveControl.Count, 0, "The GC.Alloc recorder must detect a known allocation");
            Assert.Zero(control.Count, "The allocation-free control must remain valid");
            Assert.Zero(integerWrite.Count, $"Integer writes allocated {integerWrite.Bytes} bytes");
            Assert.Zero(integerRead.Count, $"Integer reads allocated {integerRead.Bytes} bytes");
            Assert.Zero(vectorWrite.Count, $"Vector writes allocated {vectorWrite.Bytes} bytes");
            Assert.Zero(vectorRead.Count, $"Vector reads allocated {vectorRead.Bytes} bytes");
            Assert.Zero(listWrite.Count, $"List writes allocated {listWrite.Bytes} bytes");
            Assert.Zero(integerListWrite.Count, $"Integer list writes allocated {integerListWrite.Bytes} bytes");
            Assert.Zero(dictionaryWrite.Count, $"Dictionary writes allocated {dictionaryWrite.Bytes} bytes");
            Assert.Zero(integerDictionaryWrite.Count, $"Integer dictionary writes allocated {integerDictionaryWrite.Bytes} bytes");
            Assert.AreNotEqual(0, sink);
            GC.KeepAlive(allocated);
        }

        [Test]
        public void BuiltInValueCodecs_PreserveRegistryWireFormat()
        {
            AssertBuiltInValue(true);
            AssertBuiltInValue((byte)200);
            AssertBuiltInValue((sbyte)-100);
            AssertBuiltInValue('X');
            AssertBuiltInValue(2.5d);
            AssertBuiltInValue(2.5f);
            AssertBuiltInValue((short)-1234);
            AssertBuiltInValue((ushort)1234);
            AssertBuiltInValue(-123456);
            AssertBuiltInValue(123456u);
            AssertBuiltInValue(-123456789L);
            AssertBuiltInValue(123456789UL);
            AssertBuiltInValue(new Color(0.25f, 0.5f, 0.75f, 1f));
            AssertBuiltInValue(new Quaternion(1f, 2f, 3f, 4f));
            AssertBuiltInValue(new Vector2(1f, 2f));
            AssertBuiltInValue(new Vector2Int(1, 2));
            AssertBuiltInValue(new Vector3(1f, 2f, 3f));
            AssertBuiltInValue(new Vector3Int(1, 2, 3));
            AssertBuiltInValue(new Vector4(1f, 2f, 3f, 4f));
        }

        private static void AssertBuiltInValue<T>(T value)
        {
            var typedWriter = new NetDataWriter();
            var registryWriter = new NetDataWriter();
            typedWriter.PutValue(value);
            registryWriter.PutValue(typeof(T), value);
            CollectionAssert.AreEqual(registryWriter.CopyData(), typedWriter.CopyData(), typeof(T).Name);

            var typedReader = new NetDataReader(typedWriter.CopyData());
            var registryReader = new NetDataReader(registryWriter.CopyData());
            Assert.AreEqual(registryReader.GetValue(typeof(T)), typedReader.GetValue<T>(), typeof(T).Name);
        }

        [Test]
        public void CustomValueCodecs_StillOverrideBuiltInCodecs()
        {
            // Resolve the runtime assembly explicitly: the source generator also
            // emits a partial registry into the test assembly.
            var assembly = typeof(NetDataWriterExtension).Assembly;
            var writerRegistry = assembly.GetType("LiteNetLibManager.Serialization.WriterRegistry");
            var readerRegistry = assembly.GetType("LiteNetLibManager.Serialization.ReaderRegistry");
            Assert.IsNotNull(writerRegistry);
            Assert.IsNotNull(readerRegistry);
            var getWriter = writerRegistry.GetMethod("TryGetWriter");
            var getReader = readerRegistry.GetMethod("TryGetReader");
            var registerWriter = writerRegistry.GetMethod("RegisterWriter");
            var registerReader = readerRegistry.GetMethod("RegisterReader");
            object[] writerArgs = { typeof(int), null };
            object[] readerArgs = { typeof(int), null };
            Assert.IsTrue((bool)getWriter.Invoke(null, writerArgs));
            Assert.IsTrue((bool)getReader.Invoke(null, readerArgs));
            try
            {
                Action<NetDataWriter, object> customWriter = (writer, value) => writer.Put((byte)99);
                Func<NetDataReader, object> customReader = reader => reader.GetByte() + 1;
                registerWriter.Invoke(null, new object[] { typeof(int), customWriter });
                registerReader.Invoke(null, new object[] { typeof(int), customReader });

                var writer = new NetDataWriter();
                writer.PutValue(5);
                CollectionAssert.AreEqual(new byte[] { 99 }, writer.CopyData());
                Assert.AreEqual(100, new NetDataReader(writer.CopyData()).GetValue<int>());
            }
            finally
            {
                registerWriter.Invoke(null, writerArgs);
                registerReader.Invoke(null, readerArgs);
            }
        }
    }
}
