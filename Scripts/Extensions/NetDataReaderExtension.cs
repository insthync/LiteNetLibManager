using System;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public static class NetDataReaderExtension
    {
        public static TType GetValue<TType>(this NetDataReader reader)
        {
            return (TType)GetValue(reader, typeof(TType));
        }

        public static object GetValue(this NetDataReader reader, Type type)
        {
            #region Generic Values
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            if (type == typeof(bool))
                return reader.GetBool();

            if (type == typeof(byte))
                return reader.GetByte();

            if (type == typeof(char))
                return reader.GetChar();

            if (type == typeof(double))
                return reader.GetDouble();

            if (type == typeof(float))
                return reader.GetFloat();

            if (type == typeof(int))
                return reader.GetPackedInt();

            if (type == typeof(long))
                return reader.GetPackedLong();

            if (type == typeof(sbyte))
                return reader.GetSByte();

            if (type == typeof(short))
                return reader.GetPackedShort();

            if (type == typeof(string))
                return reader.GetString();

            if (type == typeof(uint))
                return reader.GetPackedUInt();

            if (type == typeof(ulong))
                return reader.GetPackedULong();

            if (type == typeof(ushort))
                return reader.GetPackedUShort();
            #endregion

            if (typeof(INetSerializable).IsAssignableFrom(type))
            {
                object instance = Activator.CreateInstance(type);
                (instance as INetSerializable).Deserialize(reader);
                return instance;
            }

            throw new ArgumentException("NetDataReader cannot read type " + type.Name);
        }

        public static TValue[] GetArray<TValue>(this NetDataReader reader)
        {
            int count = reader.GetInt();
            TValue[] result = new TValue[count];
            for (int i = 0; i < count; ++i)
            {
                result[i] = reader.GetValue<TValue>();
            }
            return result;
        }

        public static object GetArray(this NetDataReader reader, Type type)
        {
            int count = reader.GetInt();
            Array array = Array.CreateInstance(type, count);
            for (int i = 0; i < count; ++i)
            {
                array.SetValue(reader.GetValue(type), i);
            }
            return array;
        }

        public static List<TValue> GetList<TValue>(this NetDataReader reader)
        {
            int count = reader.GetInt();
            List<TValue> result = new List<TValue>();
            for (int i = 0; i < count; ++i)
            {
                result.Add(reader.GetValue<TValue>());
            }
            return result;
        }

        public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(this NetDataReader reader)
        {
            int count = reader.GetInt();
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            for (int i = 0; i < count; ++i)
            {
                result.Add(reader.GetValue<TKey>(), reader.GetValue<TValue>());
            }
            return result;
        }

        #region Packed Signed Int (Ref: https://developers.google.com/protocol-buffers/docs/encoding#signed-integers)
        public static short GetPackedShort(this NetDataReader reader)
        {
            return (short)GetPackedInt(reader);
        }

        public static int GetPackedInt(this NetDataReader reader)
        {
            uint value = GetPackedUInt(reader);
            return (int)((value >> 1) ^ (-(int)(value & 1)));
        }

        public static long GetPackedLong(this NetDataReader reader)
        {
            return ((long)GetPackedInt(reader)) << 32 | ((uint)GetPackedInt(reader));
        }
        #endregion

        #region Packed Unsigned Int (Ref: https://sqlite.org/src4/doc/trunk/www/varint.wiki)
        public static ushort GetPackedUShort(this NetDataReader reader)
        {
            return (ushort)GetPackedULong(reader);
        }

        public static uint GetPackedUInt(this NetDataReader reader)
        {
            return (uint)GetPackedULong(reader);
        }

        public static ulong GetPackedULong(this NetDataReader reader)
        {
            byte a0 = reader.GetByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = reader.GetByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + 256 * (a0 - ((ulong)241)) + a1;
            }

            byte a2 = reader.GetByte();
            if (a0 == 249)
            {
                return 2288 + (((ulong)256) * a1) + a2;
            }

            byte a3 = reader.GetByte();
            if (a0 == 250)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16);
            }

            byte a4 = reader.GetByte();
            if (a0 == 251)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24);
            }

            byte a5 = reader.GetByte();
            if (a0 == 252)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32);
            }

            byte a6 = reader.GetByte();
            if (a0 == 253)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40);
            }

            byte a7 = reader.GetByte();
            if (a0 == 254)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48);
            }

            byte a8 = reader.GetByte();
            if (a0 == 255)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48) + (((ulong)a8) << 56);
            }
            throw new System.IndexOutOfRangeException("ReadPackedULong() failure: " + a0);
        }
        #endregion
    }
}
