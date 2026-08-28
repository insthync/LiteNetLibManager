using LiteNetLib.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager.Tests
{
    public class SerializationRoundTripTests
    {
        private NetDataWriter _writer;
        private NetDataReader _reader;

        [SetUp]
        public void SetUp()
        {
            _writer = new NetDataWriter();
            _reader = new NetDataReader();
        }

        private object RoundTrip(Type type, object value)
        {
            _writer.Reset();
            _writer.PutValue(type, value);
            _reader.SetSource(_writer.CopyData());
            return _reader.GetValue(type);
        }

        private void RoundTrip<TType>(TType value)
        {
            object result = RoundTrip(typeof(TType), value);
            Assert.AreEqual(value, result, $"Round trip failed for {typeof(TType).Name}");
        }

        [Test]
        public void Float_RoundTripsExactly()
        {
            // Regression test: WriteSingle used to cast the boxed float to bool
            // (WriterRegistry.WriteSingle), which threw InvalidCastException and
            // wrote 1 byte instead of the 4 bytes the reader expects.
            RoundTrip(0f);
            RoundTrip(1f);
            RoundTrip(-1f);
            RoundTrip(123.456f);
            RoundTrip(-123.456f);
            RoundTrip(float.Epsilon);
            RoundTrip(float.MinValue);
            RoundTrip(float.MaxValue);
        }

        [Test]
        public void FloatArray_RoundTripsExactly()
        {
            float[] value = new float[] { 0f, 1.5f, -123.456f, float.MaxValue };
            object result = RoundTripWithArray(typeof(float), value);
            Assert.AreEqual(value, (float[])result);
        }

        [Test]
        public void Bool_RoundTrips()
        {
            RoundTrip(true);
            RoundTrip(false);
        }

        [Test]
        public void Byte_RoundTrips()
        {
            RoundTrip(byte.MinValue);
            RoundTrip((byte)127);
            RoundTrip(byte.MaxValue);
        }

        [Test]
        public void SByte_RoundTrips()
        {
            RoundTrip(sbyte.MinValue);
            RoundTrip((sbyte)0);
            RoundTrip(sbyte.MaxValue);
        }

        [Test]
        public void Char_RoundTrips()
        {
            RoundTrip('A');
            RoundTrip('ส');
        }

        [Test]
        public void Double_RoundTripsExactly()
        {
            RoundTrip(0.0);
            RoundTrip(3.141592653589793);
            RoundTrip(double.MinValue);
            RoundTrip(double.MaxValue);
        }

        [Test]
        public void SignedIntegers_RoundTrips()
        {
            RoundTrip(short.MinValue);
            RoundTrip(short.MaxValue);
            RoundTrip(int.MinValue);
            RoundTrip(-1);
            RoundTrip(0);
            RoundTrip(123456789);
            RoundTrip(int.MaxValue);
            RoundTrip(long.MinValue);
            RoundTrip(long.MaxValue);
        }

        [Test]
        public void UnsignedIntegers_RoundTrips()
        {
            RoundTrip(ushort.MinValue);
            RoundTrip(ushort.MaxValue);
            RoundTrip(uint.MinValue);
            RoundTrip(uint.MaxValue);
            RoundTrip(ulong.MinValue);
            RoundTrip(ulong.MaxValue);
        }

        [Test]
        public void String_RoundTrips()
        {
            RoundTrip(string.Empty);
            RoundTrip("Hello, World!");
            RoundTrip("สวัสดีครับ");
        }

        [Test]
        public void Enum_RoundTrips()
        {
            object result = RoundTrip(typeof(TestEnum), TestEnum.LargeValue);
            Assert.AreEqual(TestEnum.LargeValue, (TestEnum)result);
        }

        [Test]
        public void Color_RoundTripsWithinQuantizationError()
        {
            // Color is quantized to 1/100 steps per channel
            object result = RoundTrip(typeof(Color), new Color(0.25f, 0.5f, 0.75f, 1f));
            Color color = (Color)result;
            Assert.AreEqual(0.25f, color.r, 0.011f);
            Assert.AreEqual(0.5f, color.g, 0.011f);
            Assert.AreEqual(0.75f, color.b, 0.011f);
            Assert.AreEqual(1f, color.a, 0.011f);
        }

        [Test]
        public void Quaternion_RoundTripsExactly()
        {
            RoundTrip(Quaternion.identity);
            RoundTrip(new Quaternion(0.1f, 0.2f, 0.3f, 0.4f));
            RoundTrip(Quaternion.Euler(30f, 60f, 90f));
        }

        [Test]
        public void Vectors_RoundTripsExactly()
        {
            RoundTrip(new Vector2(1.5f, -2.5f));
            RoundTrip(new Vector2Int(int.MinValue, int.MaxValue));
            RoundTrip(new Vector3(1.5f, -2.5f, 3.25f));
            RoundTrip(new Vector3Int(-1, 0, 1));
            RoundTrip(new Vector4(1.5f, -2.5f, 3.25f, -4.75f));
        }

        [Test]
        public void IntArray_RoundTrips()
        {
            int[] value = new int[] { -1, 0, 1, int.MinValue, int.MaxValue };
            object result = RoundTripWithArray(typeof(int), value);
            Assert.AreEqual(value, (int[])result);
        }

        [Test]
        public void StringArray_RoundTrips()
        {
            string[] value = new string[] { string.Empty, "one", "สอง" };
            object result = RoundTripWithArray(typeof(string), value);
            Assert.AreEqual(value, (string[])result);
        }

        [Test]
        public void NullArray_WritesEmptyArray()
        {
            object result = RoundTripWithArray(typeof(int), null);
            Assert.AreEqual(0, ((int[])result).Length);
        }

        [Test]
        public void Dictionary_RoundTrips()
        {
            Dictionary<string, int> value = new Dictionary<string, int>()
            {
                { "one", 1 },
                { "two", 2 },
            };
            _writer.Reset();
            _writer.PutDictionary(value);
            _reader.SetSource(_writer.CopyData());
            Dictionary<string, int> result = _reader.GetDictionary<string, int>();
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result["one"]);
            Assert.AreEqual(2, result["two"]);
        }

        private object RoundTripWithArray(Type elementType, object array)
        {
            _writer.Reset();
            _writer.PutArrayObject(elementType, array);
            _reader.SetSource(_writer.CopyData());
            return _reader.GetArrayObject(elementType);
        }

        private enum TestEnum : byte
        {
            None = 0,
            SmallValue = 1,
            LargeValue = 200,
        }
    }
}
