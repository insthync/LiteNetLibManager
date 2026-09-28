using System;

namespace LiteNetLib.Utils
{
    internal static class NetDataCollectionLimits
    {
        // Bounds allocations and iteration counts derived from untrusted packets.
        internal const int MaxElementCount = ushort.MaxValue;

        internal static int ReadCount(NetDataReader reader)
        {
            int count = reader.GetInt();
            if (count < 0 || count > MaxElementCount)
                throw new InvalidOperationException($"Invalid network collection count: {count}");
            return count;
        }

        internal static void ValidateWriteCount(int count)
        {
            if (count < 0 || count > MaxElementCount)
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    $"Network collections cannot contain more than {MaxElementCount} elements.");
        }
    }
}
