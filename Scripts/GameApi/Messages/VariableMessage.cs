using System;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class VariableMessage<TType> : INetSerializable
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
    public class BoolMessage : VariableMessage<bool>
    {
    }

    [Serializable]
    public class BoolArrayMessage : VariableMessage<bool[]>
    {
    }

    [Serializable]
    public class ByteMessage : VariableMessage<byte>
    {
    }

    [Serializable]
    public class ByteArrayMessage : VariableMessage<byte[]>
    {
    }

    [Serializable]
    public class CharMessage : VariableMessage<char>
    {
    }

    [Serializable]
    public class DoubleMessage : VariableMessage<double>
    {
    }

    [Serializable]
    public class DoubleArrayMessage : VariableMessage<double[]>
    {
    }

    [Serializable]
    public class FloatMessage : VariableMessage<float>
    {
    }

    [Serializable]
    public class FloatArrayMessage : VariableMessage<float[]>
    {
    }

    [Serializable]
    public class IntMessage : VariableMessage<int>
    {
    }

    [Serializable]
    public class IntArrayMessage : VariableMessage<int[]>
    {
    }

    [Serializable]
    public class LongMessage : VariableMessage<long>
    {
    }

    [Serializable]
    public class LongArrayMessage : VariableMessage<long[]>
    {
    }

    [Serializable]
    public class SByteMessage : VariableMessage<sbyte>
    {
    }

    [Serializable]
    public class ShortMessage : VariableMessage<short>
    {
    }

    [Serializable]
    public class ShortArrayMessage : VariableMessage<short[]>
    {
    }

    [Serializable]
    public class StringMessage : VariableMessage<string>
    {
    }

    [Serializable]
    public class UIntMessage : VariableMessage<uint>
    {
    }

    [Serializable]
    public class UIntArrayMessage : VariableMessage<uint[]>
    {
    }

    [Serializable]
    public class ULongMessage : VariableMessage<ulong>
    {
    }

    [Serializable]
    public class ULongArrayMessage : VariableMessage<ulong[]>
    {
    }

    [Serializable]
    public class UShortMessage : VariableMessage<ushort>
    {
    }

    [Serializable]
    public class UShortArrayMessage : VariableMessage<ushort[]>
    {
    }

    [Serializable]
    public class ColorMessage : VariableMessage<Color>
    {
    }

    [Serializable]
    public class QuaternionMessage : VariableMessage<Quaternion>
    {
    }

    [Serializable]
    public class Vector2Message : VariableMessage<Vector2>
    {
    }

    [Serializable]
    public class Vector2IntMessage : VariableMessage<Vector2Int>
    {
    }

    [Serializable]
    public class Vector3Message : VariableMessage<Vector3>
    {
    }

    [Serializable]
    public class Vector3IntMessage : VariableMessage<Vector3Int>
    {
    }

    [Serializable]
    public class Vector4Message : VariableMessage<Vector4>
    {
    }

    [Serializable]
    public class PackedUShortMessage : VariableMessage<PackedUShort>
    {
    }

    [Serializable]
    public class PackedUIntMessage : VariableMessage<PackedUInt>
    {
    }

    [Serializable]
    public class PackedULongMessage : VariableMessage<PackedULong>
    {
    }
    #endregion
}
