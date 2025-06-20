using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibElement
    {
        public bool IsSetup { get; private set; }

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
            get { return !IsSetup ? -1 : Identity.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return !IsSetup ? 0 : Identity.ObjectId; }
        }

        public byte SyncChannelId
        {
            get { return !IsSetup ? (byte)0 : Identity.SyncChannelId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return Identity.Manager; }
        }

        public LiteNetLibPlayer Player
        {
            get { return Identity.Player; }
        }

        public bool IsServer
        {
            get { return IsSetup && Identity.IsServer; }
        }

        public bool IsClient
        {
            get { return IsSetup && Identity.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return IsSetup && Identity.IsOwnerClient; }
        }

        public bool IsOwnerHost
        {
            get { return IsSetup && Identity.IsOwnerHost; }
        }

        public bool IsOwnedByServer
        {
            get { return IsSetup && Identity.IsOwnedByServer; }
        }

        public bool IsOwnerClientOrOwnedByServer
        {
            get { return IsSetup && Identity.IsOwnerClientOrOwnedByServer; }
        }

        public bool IsSceneObject
        {
            get { return IsSetup && Identity.IsSceneObject; }
        }

        [ReadOnly, SerializeField]
        protected int _elementId;
        public int ElementId
        {
            get { return _elementId; }
        }

        public virtual string LogTag
        {
            get
            {
                return (!IsSetup ? LiteNetLibBehaviour.TAG_NULL : Behaviour.LogTag) + ".E";
            }
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
    }
}
