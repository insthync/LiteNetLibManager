using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncElement : LiteNetLibElement, System.IEquatable<LiteNetLibSyncElement>
    {
        public abstract byte ElementType { get; }

        internal virtual bool CanSyncFromServer(LiteNetLibPlayer player)
        {
            return Identity.Subscribers.Contains(player.ConnectionId);
        }

        internal virtual bool CanSyncFromOwnerClient()
        {
            return false;
        }

        internal virtual bool WillSyncFromServerReliably(LiteNetLibPlayer player, uint tick)
        {
            // Always sync reliably by default
            return true;
        }

        internal virtual bool WillSyncFromServerUnreliably(LiteNetLibPlayer player, uint tick)
        {
            // Always sync reliably by default
            return false;
        }

        internal virtual bool WillSyncFromOwnerClientReliably(uint tick)
        {
            // Not be able to be sent from client by default
            return false;
        }

        internal virtual bool WillSyncFromOwnerClientUnreliably(uint tick)
        {
            // Not be able to be sent from client by default
            return false;
        }

        internal abstract void Reset();
        internal abstract void WriteSyncData(bool isState, uint tick, bool initial, NetDataWriter writer);
        internal abstract void ReadSyncData(bool isState, uint tick, bool initial, NetDataReader reader);

        public void RegisterUpdating()
        {
            if (!IsSpawned)
                return;
            if (Manager == null)
                return;
            if (Manager.IsServer)
                Manager.RegisterServerSyncElement(this);
            else
                Manager.RegisterClientSyncElement(this);
        }

        public void UnregisterUpdating()
        {
            if (Manager == null)
                return;
            if (Manager.IsServer)
                Manager.UnregisterServerSyncElement(this);
            else
                Manager.UnregisterClientSyncElement(this);
        }

        public virtual void Synced(uint tick)
        {
            UnregisterUpdating();
        }

        public bool Equals(LiteNetLibSyncElement other)
        {
            return IsSpawned == other.IsSpawned &&
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
                hash = hash * 31 + IsSpawned.GetHashCode();
                hash = hash * 31 + ElementType.GetHashCode();
                hash = hash * 31 + ObjectId.GetHashCode();
                hash = hash * 31 + ElementId.GetHashCode();
                return hash;
            }
        }
    }
}
