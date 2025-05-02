using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncElement : LiteNetLibElement, System.IEquatable<LiteNetLibSyncElement>
    {
        public abstract byte ElementType { get; }
        internal virtual bool WillSyncData(LiteNetLibPlayer player)
        {
            // No pending spawn/despawn for this player
            if (player.NetworkSpawningObjectIds.Contains(ObjectId) ||
                player.NetworkDestroyingObjectIds.ContainsKey(ObjectId))
                return false;
            // Don't sync data if player not subscribe the object
            return Identity.Subscribers.Contains(player.ConnectionId);
        }

        internal abstract void WriteSyncData(NetDataWriter writer);
        internal abstract void ReadSyncData(NetDataReader reader);

        public bool Equals(LiteNetLibSyncElement other)
        {
            return IsSetup == other.IsSetup &&
                ElementType == other.ElementType &&
                SyncChannelId == other.SyncChannelId &&
                ObjectId == other.ObjectId &&
                ElementId == other.ElementId;
        }

        public override bool Equals(object obj)
        {
            return obj is LiteNetLibSyncElement other && Equals(other);
        }

        public override int GetHashCode()
        {
            /*
             * unchecked: avoids exceptions on overflow (safe for hash codes).
             * 17 and 31 are classic seed and multiplier primes (good hash distribution).
             */
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + IsSetup.GetHashCode();
                hash = hash * 31 + ElementType.GetHashCode();
                hash = hash * 31 + ObjectId.GetHashCode();
                hash = hash * 31 + ElementId.GetHashCode();
                return hash;
            }
        }
    }
}