using LiteNetLib.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiteNetLibManager.Tests
{
    public class NetDataExtensionTests
    {
        private enum ByteCode : byte { Value = 200 }
        private enum IntCode : int { Value = -12345 }

        // Generic types are intentionally skipped by the registry source generator,
        // so this exercises the extensions' INetSerializable fallback.
        public class SerializableValue<T> : INetSerializable
        {
            public int Number;

            public void Serialize(NetDataWriter writer) => writer.PutPackedInt(Number);
            public void Deserialize(NetDataReader reader) => Number = reader.GetPackedInt();
        }

        private static NetDataReader Reader(NetDataWriter writer) => new NetDataReader(writer.CopyData());

        [Test]
        public void GenericValue_UsesRegisteredReaderAndWriter()
        {
            var writer = new NetDataWriter();
            writer.PutValue(-12345);
            var reader = Reader(writer);
            Assert.AreEqual(-12345, reader.GetValue<int>());
            Assert.IsTrue(reader.EndOfData);
        }

        [Test]
        public void TypeValue_UsesRegisteredReaderAndWriter()
        {
            var writer = new NetDataWriter();
            writer.PutValue(typeof(string), "network");
            var reader = Reader(writer);
            Assert.AreEqual("network", reader.GetValue(typeof(string)));
            Assert.IsTrue(reader.EndOfData);
        }

        [Test]
        public void EnumValues_UseTheirUnderlyingWidthsAndReturnEnumValues()
        {
            var writer = new NetDataWriter();
            writer.PutValue(ByteCode.Value);
            writer.PutValue(typeof(IntCode), IntCode.Value);
            var reader = Reader(writer);
            Assert.AreEqual(ByteCode.Value, reader.GetValue<ByteCode>());
            Assert.AreEqual(IntCode.Value, reader.GetValue(typeof(IntCode)));
            Assert.IsTrue(reader.EndOfData);
        }

        [Test]
        public void SerializableValue_UsesFallbackForBothOverloads()
        {
            LogAssert.Expect(LogType.Warning, $"No writer registered for type: {typeof(SerializableValue<int>).FullName}");
            var writer = new NetDataWriter();
            writer.PutValue(new SerializableValue<int> { Number = -7 });
            LogAssert.Expect(LogType.Warning, $"No reader registered for type: {typeof(SerializableValue<int>).FullName}");
            Assert.AreEqual(-7, Reader(writer).GetValue<SerializableValue<int>>().Number);

            writer.Reset();
            LogAssert.Expect(LogType.Warning, $"No writer registered for type: {typeof(SerializableValue<int>).FullName}");
            writer.PutValue(typeof(SerializableValue<int>), new SerializableValue<int> { Number = 42 });
            LogAssert.Expect(LogType.Warning, $"No reader registered for type: {typeof(SerializableValue<int>).FullName}");
            Assert.AreEqual(42, ((SerializableValue<int>)Reader(writer).GetValue(typeof(SerializableValue<int>))).Number);
        }

        [Test]
        public void UnsupportedValue_ThrowsOnReadAndWrite()
        {
            LogAssert.Expect(LogType.Warning, $"No writer registered for type: {typeof(Guid).FullName}");
            Assert.Throws<ArgumentException>(() => new NetDataWriter().PutValue(Guid.NewGuid()));
            LogAssert.Expect(LogType.Warning, $"No reader registered for type: {typeof(Guid).FullName}");
            Assert.Throws<ArgumentException>(() => new NetDataReader().GetValue<Guid>());
        }

        [Test]
        public void Color_UsesFourQuantizedBytes()
        {
            var writer = new NetDataWriter();
            writer.PutColor(new Color(0.25f, 0.5f, 0.75f, 1f));
            CollectionAssert.AreEqual(new byte[] { 25, 50, 75, 100 }, writer.CopyData());

            var color = new NetDataReader(new byte[] { 25, 50, 75, 100 }).GetColor();
            Assert.AreEqual(0.25f, color.r, 0.0001f);
            Assert.AreEqual(0.5f, color.g, 0.0001f);
            Assert.AreEqual(0.75f, color.b, 0.0001f);
            Assert.AreEqual(1f, color.a, 0.0001f);
        }

        [Test]
        public void Quaternion_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutQuaternion(new Quaternion(1.25f, -2.5f, 3.75f, -4.5f));
            var reader = Reader(writer);
            Assert.AreEqual(1.25f, reader.GetFloat());
            Assert.AreEqual(-2.5f, reader.GetFloat());
            Assert.AreEqual(3.75f, reader.GetFloat());
            Assert.AreEqual(-4.5f, reader.GetFloat());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(1.25f);
            writer.Put(-2.5f);
            writer.Put(3.75f);
            writer.Put(-4.5f);
            Assert.AreEqual(new Quaternion(1.25f, -2.5f, 3.75f, -4.5f), Reader(writer).GetQuaternion());
        }

        [Test]
        public void Vector2_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutVector2(new Vector2(1.25f, -2.5f));
            var reader = Reader(writer);
            Assert.AreEqual(1.25f, reader.GetFloat());
            Assert.AreEqual(-2.5f, reader.GetFloat());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(1.25f);
            writer.Put(-2.5f);
            Assert.AreEqual(new Vector2(1.25f, -2.5f), Reader(writer).GetVector2());
        }

        [Test]
        public void Vector2Int_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutVector2Int(new Vector2Int(int.MinValue, int.MaxValue));
            var reader = Reader(writer);
            Assert.AreEqual(int.MinValue, reader.GetInt());
            Assert.AreEqual(int.MaxValue, reader.GetInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(int.MinValue);
            writer.Put(int.MaxValue);
            Assert.AreEqual(new Vector2Int(int.MinValue, int.MaxValue), Reader(writer).GetVector2Int());
        }

        [Test]
        public void Vector3_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutVector3(new Vector3(1.25f, -2.5f, 3.75f));
            var reader = Reader(writer);
            Assert.AreEqual(1.25f, reader.GetFloat());
            Assert.AreEqual(-2.5f, reader.GetFloat());
            Assert.AreEqual(3.75f, reader.GetFloat());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(1.25f);
            writer.Put(-2.5f);
            writer.Put(3.75f);
            Assert.AreEqual(new Vector3(1.25f, -2.5f, 3.75f), Reader(writer).GetVector3());
        }

        [Test]
        public void Vector3Int_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutVector3Int(new Vector3Int(int.MinValue, 0, int.MaxValue));
            var reader = Reader(writer);
            Assert.AreEqual(int.MinValue, reader.GetInt());
            Assert.AreEqual(0, reader.GetInt());
            Assert.AreEqual(int.MaxValue, reader.GetInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(int.MinValue);
            writer.Put(0);
            writer.Put(int.MaxValue);
            Assert.AreEqual(new Vector3Int(int.MinValue, 0, int.MaxValue), Reader(writer).GetVector3Int());
        }

        [Test]
        public void Vector4_WritesAndReadsComponentsInOrder()
        {
            var writer = new NetDataWriter();
            writer.PutVector4(new Vector4(1.25f, -2.5f, 3.75f, -4.5f));
            var reader = Reader(writer);
            Assert.AreEqual(1.25f, reader.GetFloat());
            Assert.AreEqual(-2.5f, reader.GetFloat());
            Assert.AreEqual(3.75f, reader.GetFloat());
            Assert.AreEqual(-4.5f, reader.GetFloat());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(1.25f);
            writer.Put(-2.5f);
            writer.Put(3.75f);
            writer.Put(-4.5f);
            Assert.AreEqual(new Vector4(1.25f, -2.5f, 3.75f, -4.5f), Reader(writer).GetVector4());
        }

        [Test]
        public void GenericArray_HandlesValuesAndNullAsEmpty()
        {
            var writer = new NetDataWriter();
            writer.PutArrayExtension(new[] { -1, 0, 1000 });
            var reader = Reader(writer);
            Assert.AreEqual(3, reader.GetInt());
            Assert.AreEqual(-1, reader.GetPackedInt());
            Assert.AreEqual(0, reader.GetPackedInt());
            Assert.AreEqual(1000, reader.GetPackedInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(2);
            writer.PutPackedInt(5);
            writer.PutPackedInt(-6);
            CollectionAssert.AreEqual(new[] { 5, -6 }, Reader(writer).GetArrayExtension<int>());

            writer.Reset();
            writer.PutArrayExtension<int>(null);
            CollectionAssert.IsEmpty(Reader(writer).GetArrayExtension<int>());
            Assert.AreEqual(4, writer.Length);
        }

        [Test]
        public void ObjectArray_HandlesValuesAndNullAsEmpty()
        {
            var writer = new NetDataWriter();
            writer.PutArrayObject(typeof(int), new[] { -1, 1000 });
            var reader = Reader(writer);
            Assert.AreEqual(2, reader.GetInt());
            Assert.AreEqual(-1, reader.GetPackedInt());
            Assert.AreEqual(1000, reader.GetPackedInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(2);
            writer.PutPackedInt(5);
            writer.PutPackedInt(-6);
            CollectionAssert.AreEqual(new[] { 5, -6 }, (int[])Reader(writer).GetArrayObject(typeof(int)));

            writer.Reset();
            writer.PutArrayObject(typeof(int), null);
            CollectionAssert.IsEmpty((int[])Reader(writer).GetArrayObject(typeof(int)));
            Assert.AreEqual(4, writer.Length);
        }

        [Test]
        public void List_HandlesValuesAndNullAsEmpty()
        {
            var writer = new NetDataWriter();
            writer.PutList((IList<int>)new List<int> { -1, 1000 });
            var reader = Reader(writer);
            Assert.AreEqual(2, reader.GetInt());
            Assert.AreEqual(-1, reader.GetPackedInt());
            Assert.AreEqual(1000, reader.GetPackedInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(2);
            writer.PutPackedInt(5);
            writer.PutPackedInt(-6);
            CollectionAssert.AreEqual(new[] { 5, -6 }, Reader(writer).GetList<int>());

            writer.Reset();
            writer.PutList<int>(null);
            CollectionAssert.IsEmpty(Reader(writer).GetList<int>());
            Assert.AreEqual(4, writer.Length);
        }

        [Test]
        public void Dictionary_HandlesValuesAndNullAsEmpty()
        {
            var writer = new NetDataWriter();
            writer.PutDictionary((IDictionary<int, int>)new Dictionary<int, int> { { 5, -6 } });
            var reader = Reader(writer);
            Assert.AreEqual(1, reader.GetInt());
            Assert.AreEqual(5, reader.GetPackedInt());
            Assert.AreEqual(-6, reader.GetPackedInt());
            Assert.IsTrue(reader.EndOfData);

            writer.Reset();
            writer.Put(1);
            writer.PutPackedInt(7);
            writer.PutPackedInt(8);
            Assert.AreEqual(8, Reader(writer).GetDictionary<int, int>()[7]);

            writer.Reset();
            writer.PutDictionary<int, int>(null);
            CollectionAssert.IsEmpty(Reader(writer).GetDictionary<int, int>());
            Assert.AreEqual(4, writer.Length);
        }
    }
}
