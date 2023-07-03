using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLib.Utils
{
    public static class NetDataWriterExtension
    {
        public static void PutValue<TType>(this NetDataWriter writer, TType value)
        {
            writer.PutValue(typeof(TType), value);
        }

        public static void PutValue(this NetDataWriter writer, Type type, object value)
        {
            #region Generic Values
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            if (type == typeof(bool))
            {
                writer.Put((bool)value);
                return;
            }

            if (type == typeof(byte))
            {
                writer.Put((byte)value);
                return;
            }

            if (type == typeof(char))
            {
                writer.Put((char)value);
                return;
            }

            if (type == typeof(double))
            {
                writer.Put((double)value);
                return;
            }

            if (type == typeof(float))
            {
                writer.Put((float)value);
                return;
            }

            if (type == typeof(int))
            {
                writer.PutPackedInt((int)value);
                return;
            }

            if (type == typeof(long))
            {
                writer.PutPackedLong((long)value);
                return;
            }

            if (type == typeof(sbyte))
            {
                writer.Put((sbyte)value);
                return;
            }

            if (type == typeof(short))
            {
                writer.PutPackedShort((short)value);
                return;
            }

            if (type == typeof(string))
            {
                writer.Put((string)value);
                return;
            }

            if (type == typeof(uint))
            {
                writer.PutPackedUInt((uint)value);
                return;
            }

            if (type == typeof(ulong))
            {
                writer.PutPackedULong((ulong)value);
                return;
            }

            if (type == typeof(ushort))
            {
                writer.PutPackedUShort((ushort)value);
                return;
            }
            #endregion

            #region Unity Values
            if (type == typeof(Color))
            {
                writer.PutColor((Color)value);
                return;
            }

            if (type == typeof(Quaternion))
            {
                writer.PutQuaternion((Quaternion)value);
                return;
            }

            if (type == typeof(Vector2))
            {
                writer.PutVector2((Vector2)value);
                return;
            }

            if (type == typeof(Vector2Int))
            {
                writer.PutVector2Int((Vector2Int)value);
                return;
            }

            if (type == typeof(Vector3))
            {
                writer.PutVector3((Vector3)value);
                return;
            }

            if (type == typeof(Vector3Int))
            {
                writer.PutVector3Int((Vector3Int)value);
                return;
            }

            if (type == typeof(Vector4))
            {
                writer.PutVector4((Vector4)value);
                return;
            }
            #endregion

            if (typeof(INetSerializable).IsAssignableFrom(type))
            {
                (value as INetSerializable).Serialize(writer);
                return;
            }

            throw new ArgumentException("NetDataWriter cannot write type " + value.GetType().Name);
        }

        public static void PutColor(this NetDataWriter writer, Color value)
        {
            byte r = (byte)(value.r * 100f);
            byte g = (byte)(value.g * 100f);
            byte b = (byte)(value.b * 100f);
            byte a = (byte)(value.a * 100f);
            writer.Put(r);
            writer.Put(g);
            writer.Put(b);
            writer.Put(a);
        }

        public static void PutQuaternion(this NetDataWriter writer, Quaternion value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
            writer.Put(value.w);
        }

        public static void PutVector2(this NetDataWriter writer, Vector2 value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
        }

        public static void PutVector2Int(this NetDataWriter writer, Vector2Int value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
        }

        public static void PutVector3(this NetDataWriter writer, Vector3 value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
        }

        public static void PutVector3Int(this NetDataWriter writer, Vector3Int value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
        }

        public static void PutVector4(this NetDataWriter writer, Vector4 value)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
            writer.Put(value.w);
        }

        public static void PutList<TValue>(this NetDataWriter writer, IList<TValue> list)
        {
            if (list == null)
            {
                writer.Put(0);
                return;
            }
            writer.Put(list.Count);
            foreach (var value in list)
            {
                writer.PutValue(value);
            }
        }

        public static void PutDictionary<TKey, TValue>(this NetDataWriter writer, IDictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                writer.Put(0);
                return;
            }
            writer.Put(dict.Count);
            foreach (var keyValuePair in dict)
            {
                writer.PutValue(keyValuePair.Key);
                writer.PutValue(keyValuePair.Value);
            }
        }

        #region Packed Signed Int (Ref: https://developers.google.com/protocol-buffers/docs/encoding#signed-integers)
        public static void PutPackedShort(this NetDataWriter writer, short value)
        {
            PutPackedInt(writer, value);
        }

        public static void PutPackedInt(this NetDataWriter writer, int value)
        {
            PutPackedUInt(writer, (uint)((value << 1) ^ (value >> 31)));
        }

        public static void PutPackedLong(this NetDataWriter writer, long value)
        {
            PutPackedInt(writer, (int)(value >> 32));
            PutPackedInt(writer, (int)(value & uint.MaxValue));
        }
        #endregion

        #region Packed Unsigned Int (Ref: https://sqlite.org/src4/doc/trunk/www/varint.wiki)
        public static void PutPackedUShort(this NetDataWriter writer, ushort value)
        {
            PutPackedULong(writer, value);
        }

        public static void PutPackedUInt(this NetDataWriter writer, uint value)
        {
            PutPackedULong(writer, value);
        }

        public static void PutPackedULong(this NetDataWriter writer, ulong value)
        {
            if (value <= 240)
            {
                writer.Put((byte)value);
                return;
            }
            if (value <= 2287)
            {
                writer.Put((byte)((value - 240) / 256 + 241));
                writer.Put((byte)((value - 240) % 256));
                return;
            }
            if (value <= 67823)
            {
                writer.Put((byte)249);
                writer.Put((byte)((value - 2288) / 256));
                writer.Put((byte)((value - 2288) % 256));
                return;
            }
            if (value <= 16777215)
            {
                writer.Put((byte)250);
                writer.Put((byte)(value & 0xFF));
                writer.Put((byte)((value >> 8) & 0xFF));
                writer.Put((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                writer.Put((byte)251);
                writer.Put((byte)(value & 0xFF));
                writer.Put((byte)((value >> 8) & 0xFF));
                writer.Put((byte)((value >> 16) & 0xFF));
                writer.Put((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                writer.Put((byte)252);
                writer.Put((byte)(value & 0xFF));
                writer.Put((byte)((value >> 8) & 0xFF));
                writer.Put((byte)((value >> 16) & 0xFF));
                writer.Put((byte)((value >> 24) & 0xFF));
                writer.Put((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                writer.Put((byte)253);
                writer.Put((byte)(value & 0xFF));
                writer.Put((byte)((value >> 8) & 0xFF));
                writer.Put((byte)((value >> 16) & 0xFF));
                writer.Put((byte)((value >> 24) & 0xFF));
                writer.Put((byte)((value >> 32) & 0xFF));
                writer.Put((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                writer.Put((byte)254);
                writer.Put((byte)(value & 0xFF));
                writer.Put((byte)((value >> 8) & 0xFF));
                writer.Put((byte)((value >> 16) & 0xFF));
                writer.Put((byte)((value >> 24) & 0xFF));
                writer.Put((byte)((value >> 32) & 0xFF));
                writer.Put((byte)((value >> 40) & 0xFF));
                writer.Put((byte)((value >> 48) & 0xFF));
                return;
            }
            // all others
            writer.Put((byte)255);
            writer.Put((byte)(value & 0xFF));
            writer.Put((byte)((value >> 8) & 0xFF));
            writer.Put((byte)((value >> 16) & 0xFF));
            writer.Put((byte)((value >> 24) & 0xFF));
            writer.Put((byte)((value >> 32) & 0xFF));
            writer.Put((byte)((value >> 40) & 0xFF));
            writer.Put((byte)((value >> 48) & 0xFF));
            writer.Put((byte)((value >> 56) & 0xFF));
        }
        #endregion
    }
}
