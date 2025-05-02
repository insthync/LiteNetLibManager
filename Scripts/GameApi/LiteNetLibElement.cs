using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibElement : System.IEquatable<LiteNetLibElement>
    {
        public bool IsSetup { get; private set; }
        public abstract byte ElementType { get; }

        [ReadOnly, SerializeField]
        protected LiteNetLibBehaviour _behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return _behaviour; }
        }

        public LiteNetLibIdentity Identity
        {
            get { return Behaviour.Identity; }
        }

        public long ConnectionId
        {
            get { return !IsSetup ? -1 : Behaviour.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return !IsSetup ? 0 : Behaviour.ObjectId; }
        }

        public byte SyncChannelId
        {
            get { return !IsSetup ? (byte)0 : Behaviour.SyncChannelId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return Behaviour.Manager; }
        }

        public virtual string LogTag
        {
            get
            {
                return (!IsSetup ? LiteNetLibBehaviour.TAG_NULL : Behaviour.LogTag) + ".E";
            }
        }

        public bool IsServer
        {
            get { return IsSetup && Behaviour.IsServer; }
        }

        public bool IsClient
        {
            get { return IsSetup && Behaviour.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return IsSetup && Behaviour.IsOwnerClient; }
        }

        [ReadOnly, SerializeField]
        protected int _elementId;
        public int ElementId
        {
            get { return _elementId; }
        }

        public LiteNetLibElementInfo GetInfo()
        {
            return new LiteNetLibElementInfo()
            {
                objectId = ObjectId,
                elementId = ElementId,
            };
        }

        internal virtual void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            _behaviour = behaviour;
            _elementId = elementId;
            IsSetup = true;
        }

        protected virtual bool CanSync()
        {
            return IsSetup;
        }

        public bool Equals(LiteNetLibElement other)
        {
            return IsSetup == other.IsSetup &&
                ElementType == other.ElementType &&
                SyncChannelId == other.SyncChannelId &&
                ObjectId == other.ObjectId &&
                ElementId == other.ElementId;
        }

        public override bool Equals(object obj)
        {
            return obj is LiteNetLibElement other && Equals(other);
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
