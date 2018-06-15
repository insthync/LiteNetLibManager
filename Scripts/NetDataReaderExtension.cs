namespace LiteNetLib.Utils
{
    public static class NetDataReaderExtension
    {
        public static uint GetPackedUInt(this NetDataReader reader)
        {
            byte a0 = reader.GetByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = reader.GetByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return (uint)(240 + 256 * (a0 - 241) + a1);
            }

            byte a2 = reader.GetByte();
            if (a0 == 249)
            {
                return (uint)(2288 + 256 * a1 + a2);
            }

            byte a3 = reader.GetByte();
            if (a0 == 250)
            {
                return a1 + (((uint)a2) << 8) + (((uint)a3) << 16);
            }

            byte a4 = reader.GetByte();
            if (a0 >= 251)
            {
                return a1 + (((uint)a2) << 8) + (((uint)a3) << 16) + (((uint)a4) << 24);
            }
            throw new System.IndexOutOfRangeException("ReadPackedUInt() failure: " + a0);
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
    }
}
