using System;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class GenericValueMessage<TType> : INetSerializable
    {
        public TType value;

        public void Deserialize(NetDataReader reader)
        {
            value = (TType)reader.GetValue(typeof(TType));
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutValue(value);
        }
    }

    #region Implement for general usages and serializable
    [Serializable]
    public class BoolMessage : GenericValueMessage<bool>
    {
    }

    [Serializable]
    public class BoolArrayMessage : GenericValueMessage<bool[]>
    {
    }

    [Serializable]
    public class ByteMessage : GenericValueMessage<byte>
    {
    }

    [Serializable]
    public class ByteArrayMessage : GenericValueMessage<byte[]>
    {
    }

    [Serializable]
    public class CharMessage : GenericValueMessage<char>
    {
    }

    [Serializable]
    public class DoubleMessage : GenericValueMessage<double>
    {
    }

    [Serializable]
    public class DoubleArrayMessage : GenericValueMessage<double[]>
    {
    }

    [Serializable]
    public class FloatMessage : GenericValueMessage<float>
    {
    }

    [Serializable]
    public class FloatArrayMessage : GenericValueMessage<float[]>
    {
    }

    [Serializable]
    public class IntMessage : GenericValueMessage<int>
    {
    }

    [Serializable]
    public class IntArrayMessage : GenericValueMessage<int[]>
    {
    }

    [Serializable]
    public class LongMessage : GenericValueMessage<long>
    {
    }

    [Serializable]
    public class LongArrayMessage : GenericValueMessage<long[]>
    {
    }

    [Serializable]
    public class SByteMessage : GenericValueMessage<sbyte>
    {
    }

    [Serializable]
    public class ShortMessage : GenericValueMessage<short>
    {
    }

    [Serializable]
    public class ShortArrayMessage : GenericValueMessage<short[]>
    {
    }

    [Serializable]
    public class StringMessage : GenericValueMessage<string>
    {
    }

    [Serializable]
    public class UIntMessage : GenericValueMessage<uint>
    {
    }

    [Serializable]
    public class UIntArrayMessage : GenericValueMessage<uint[]>
    {
    }

    [Serializable]
    public class ULongMessage : GenericValueMessage<ulong>
    {
    }

    [Serializable]
    public class ULongArrayMessage : GenericValueMessage<ulong[]>
    {
    }

    [Serializable]
    public class UShortMessage : GenericValueMessage<ushort>
    {
    }

    [Serializable]
    public class UShortArrayMessage : GenericValueMessage<ushort[]>
    {
    }

    [Serializable]
    public class ColorMessage : GenericValueMessage<Color>
    {
    }

    [Serializable]
    public class QuaternionMessage : GenericValueMessage<Quaternion>
    {
    }

    [Serializable]
    public class Vector2Message : GenericValueMessage<Vector2>
    {
    }

    [Serializable]
    public class Vector2IntMessage : GenericValueMessage<Vector2Int>
    {
    }

    [Serializable]
    public class Vector3Message : GenericValueMessage<Vector3>
    {
    }

    [Serializable]
    public class Vector3IntMessage : GenericValueMessage<Vector3Int>
    {
    }

    [Serializable]
    public class Vector4Message : GenericValueMessage<Vector4>
    {
    }

    [Serializable]
    public class PackedUShortMessage : GenericValueMessage<PackedUShort>
    {
    }

    [Serializable]
    public class PackedUIntMessage : GenericValueMessage<PackedUInt>
    {
    }

    [Serializable]
    public class PackedULongMessage : GenericValueMessage<PackedULong>
    {
    }
    #endregion
}
