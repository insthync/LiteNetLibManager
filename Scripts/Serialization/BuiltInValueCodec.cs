using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager.Serialization
{
    // The object-based registry is needed for runtime types and custom serializers.
    // Generic calls can use typed delegates for the built-ins without boxing values.
    internal static class BuiltInValueCodec<T>
    {
        internal static Action<NetDataWriter, T> Writer;
        internal static Func<NetDataReader, T> Reader;
        internal static Action<NetDataWriter, object> BoxedWriter;
        internal static Func<NetDataReader, object> BoxedReader;

        static BuiltInValueCodec()
        {
            Type type = typeof(T);
            if (type == typeof(bool)) Use<bool>((w, v) => w.Put(v), r => r.GetBool(), WriterRegistry.WriteBool, ReaderRegistry.ReadBool);
            else if (type == typeof(byte)) Use<byte>((w, v) => w.Put(v), r => r.GetByte(), WriterRegistry.WriteByte, ReaderRegistry.ReadByte);
            else if (type == typeof(sbyte)) Use<sbyte>((w, v) => w.Put(v), r => r.GetSByte(), WriterRegistry.WriteSByte, ReaderRegistry.ReadSByte);
            else if (type == typeof(char)) Use<char>((w, v) => w.Put(v), r => r.GetChar(), WriterRegistry.WriteChar, ReaderRegistry.ReadChar);
            else if (type == typeof(double)) Use<double>((w, v) => w.Put(v), r => r.GetDouble(), WriterRegistry.WriteDouble, ReaderRegistry.ReadDouble);
            else if (type == typeof(float)) Use<float>((w, v) => w.Put(v), r => r.GetFloat(), WriterRegistry.WriteSingle, ReaderRegistry.ReadSingle);
            else if (type == typeof(short)) Use<short>((w, v) => w.PutPackedShort(v), r => r.GetPackedShort(), WriterRegistry.WriteInt16, ReaderRegistry.ReadInt16);
            else if (type == typeof(ushort)) Use<ushort>((w, v) => w.PutPackedUShort(v), r => r.GetPackedUShort(), WriterRegistry.WriteUInt16, ReaderRegistry.ReadUInt16);
            else if (type == typeof(int)) Use<int>((w, v) => w.PutPackedInt(v), r => r.GetPackedInt(), WriterRegistry.WriteInt32, ReaderRegistry.ReadInt32);
            else if (type == typeof(uint)) Use<uint>((w, v) => w.PutPackedUInt(v), r => r.GetPackedUInt(), WriterRegistry.WriteUInt32, ReaderRegistry.ReadUInt32);
            else if (type == typeof(long)) Use<long>((w, v) => w.PutPackedLong(v), r => r.GetPackedLong(), WriterRegistry.WriteInt64, ReaderRegistry.ReadInt64);
            else if (type == typeof(ulong)) Use<ulong>((w, v) => w.PutPackedULong(v), r => r.GetPackedULong(), WriterRegistry.WriteUInt64, ReaderRegistry.ReadUInt64);
            else if (type == typeof(Color)) Use<Color>((w, v) => w.PutColor(v), r => r.GetColor(), WriterRegistry.WriteColor, ReaderRegistry.ReadColor);
            else if (type == typeof(Quaternion)) Use<Quaternion>((w, v) => w.PutQuaternion(v), r => r.GetQuaternion(), WriterRegistry.WriteQuaternion, ReaderRegistry.ReadQuaternion);
            else if (type == typeof(Vector2)) Use<Vector2>((w, v) => w.PutVector2(v), r => r.GetVector2(), WriterRegistry.WriteVector2, ReaderRegistry.ReadVector2);
            else if (type == typeof(Vector2Int)) Use<Vector2Int>((w, v) => w.PutVector2Int(v), r => r.GetVector2Int(), WriterRegistry.WriteVector2Int, ReaderRegistry.ReadVector2Int);
            else if (type == typeof(Vector3)) Use<Vector3>((w, v) => w.PutVector3(v), r => r.GetVector3(), WriterRegistry.WriteVector3, ReaderRegistry.ReadVector3);
            else if (type == typeof(Vector3Int)) Use<Vector3Int>((w, v) => w.PutVector3Int(v), r => r.GetVector3Int(), WriterRegistry.WriteVector3Int, ReaderRegistry.ReadVector3Int);
            else if (type == typeof(Vector4)) Use<Vector4>((w, v) => w.PutVector4(v), r => r.GetVector4(), WriterRegistry.WriteVector4, ReaderRegistry.ReadVector4);
        }

        private static void Use<TValue>(Action<NetDataWriter, TValue> writer, Func<NetDataReader, TValue> reader,
            Action<NetDataWriter, object> boxedWriter, Func<NetDataReader, object> boxedReader)
        {
            Writer = (Action<NetDataWriter, T>)(object)writer;
            Reader = (Func<NetDataReader, T>)(object)reader;
            BoxedWriter = boxedWriter;
            BoxedReader = boxedReader;
        }
    }
}
