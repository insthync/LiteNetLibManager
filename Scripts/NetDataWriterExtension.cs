namespace LiteNetLib.Utils
{
    public static class NetDataWriterExtension
    {
        public static void PutPackedUInt(this NetDataWriter writer, uint value)
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
            // all other values of uint
            writer.Put((byte)251);
            writer.Put((byte)(value & 0xFF));
            writer.Put((byte)((value >> 8) & 0xFF));
            writer.Put((byte)((value >> 16) & 0xFF));
            writer.Put((byte)((value >> 24) & 0xFF));
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
    }
}
