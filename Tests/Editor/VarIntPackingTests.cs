using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibManager.Tests
{
    public class VarIntPackingTests
    {
        private NetDataWriter _writer;
        private NetDataReader _reader;

        [SetUp]
        public void SetUp()
        {
            _writer = new NetDataWriter();
            _reader = new NetDataReader();
        }

        private ulong RoundTripPackedULong(ulong value)
        {
            _writer.Reset();
            _writer.PutPackedULong(value);
            _reader.SetSource(_writer.CopyData());
            return _reader.GetPackedULong();
        }

        [Test]
        public void PackedULong_RoundTripsAtEncodingBoundaries()
        {
            // Every threshold where PutPackedULong switches to a wider encoding
            ulong[] boundaries = new ulong[]
            {
                0,
                1,
                240, 241, 2287, 2288, 67823, 67824,
                16777215, 16777216,
                4294967295, 4294967296,
                1099511627775, 1099511627776,
                281474976710655, 281474976710656,
                72057594037927935, 72057594037927936,
                ulong.MaxValue,
            };
            foreach (ulong value in boundaries)
            {
                Assert.AreEqual(value, RoundTripPackedULong(value), $"Failed for {value}");
            }
        }

        [Test]
        public void PackedUInt_RoundTrips()
        {
            _writer.Reset();
            _writer.PutPackedUInt(uint.MinValue);
            _writer.PutPackedUInt(123456789u);
            _writer.PutPackedUInt(uint.MaxValue);
            _reader.SetSource(_writer.CopyData());
            Assert.AreEqual(uint.MinValue, _reader.GetPackedUInt());
            Assert.AreEqual(123456789u, _reader.GetPackedUInt());
            Assert.AreEqual(uint.MaxValue, _reader.GetPackedUInt());
        }

        [Test]
        public void PackedInt_RoundTripsIncludingExtremes()
        {
            _writer.Reset();
            _writer.PutPackedInt(int.MinValue);
            _writer.PutPackedInt(-1);
            _writer.PutPackedInt(0);
            _writer.PutPackedInt(1);
            _writer.PutPackedInt(123456789);
            _writer.PutPackedInt(int.MaxValue);
            _reader.SetSource(_writer.CopyData());
            Assert.AreEqual(int.MinValue, _reader.GetPackedInt());
            Assert.AreEqual(-1, _reader.GetPackedInt());
            Assert.AreEqual(0, _reader.GetPackedInt());
            Assert.AreEqual(1, _reader.GetPackedInt());
            Assert.AreEqual(123456789, _reader.GetPackedInt());
            Assert.AreEqual(int.MaxValue, _reader.GetPackedInt());
        }

        [Test]
        public void PackedShort_RoundTripsIncludingExtremes()
        {
            _writer.Reset();
            _writer.PutPackedShort(short.MinValue);
            _writer.PutPackedShort(short.MaxValue);
            _reader.SetSource(_writer.CopyData());
            Assert.AreEqual(short.MinValue, _reader.GetPackedShort());
            Assert.AreEqual(short.MaxValue, _reader.GetPackedShort());
        }

        [Test]
        public void PackedLong_RoundTripsIncludingExtremes()
        {
            _writer.Reset();
            _writer.PutPackedLong(long.MinValue);
            _writer.PutPackedLong(-1L);
            _writer.PutPackedLong(0L);
            _writer.PutPackedLong(long.MaxValue);
            _reader.SetSource(_writer.CopyData());
            Assert.AreEqual(long.MinValue, _reader.GetPackedLong());
            Assert.AreEqual(-1L, _reader.GetPackedLong());
            Assert.AreEqual(0L, _reader.GetPackedLong());
            Assert.AreEqual(long.MaxValue, _reader.GetPackedLong());
        }

        [Test]
        public void SmallValues_StayCompact()
        {
            _writer.Reset();
            _writer.PutPackedUInt(240);
            Assert.AreEqual(1, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(241);
            Assert.AreEqual(2, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(2287);
            Assert.AreEqual(2, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(2288);
            Assert.AreEqual(3, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(67823);
            Assert.AreEqual(3, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(16777215);
            Assert.AreEqual(4, _writer.Length);

            _writer.Reset();
            _writer.PutPackedUInt(uint.MaxValue);
            Assert.AreEqual(5, _writer.Length);

            _writer.Reset();
            _writer.PutPackedULong(ulong.MaxValue);
            Assert.AreEqual(9, _writer.Length);
        }
    }
}
