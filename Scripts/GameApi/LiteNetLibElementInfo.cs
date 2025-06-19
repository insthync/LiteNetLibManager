using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct LiteNetLibElementInfo : System.IEquatable<LiteNetLibElementInfo>
    {
        public uint objectId;
        public int elementId;

        public static void SerializeInfo(LiteNetLibElementInfo info, NetDataWriter writer)
        {
            writer.PutPackedUInt(info.objectId);
            writer.PutPackedInt(info.elementId);
        }

        public static LiteNetLibElementInfo DeserializeInfo(NetDataReader reader)
        {
            return new LiteNetLibElementInfo()
            {
                objectId = reader.GetPackedUInt(),
                elementId = reader.GetPackedInt(),
            };
        }

        public bool Equals(LiteNetLibElementInfo other)
        {
            return objectId == other.objectId &&
                   elementId == other.elementId;
        }


        public override bool Equals(object obj)
        {
            return obj is LiteNetLibElementInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked // allows for overflow in hashing (no exception thrown)
            {
                int hash = 17; // start with a prime number
                hash = hash * 31 + objectId.GetHashCode();
                hash = hash * 31 + elementId.GetHashCode();
                return hash;
            }
        }
    }
}
