namespace LiteNetLibManager
{
    public struct DestroyObjectInfo : System.IEquatable<DestroyObjectInfo>
    {
        public uint objectId;
        public byte reasons;

        public bool Equals(DestroyObjectInfo other)
        {
            return objectId == other.objectId;
        }

        public override bool Equals(object obj)
        {
            return obj is DestroyObjectInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return objectId.GetHashCode();
        }
    }
}
