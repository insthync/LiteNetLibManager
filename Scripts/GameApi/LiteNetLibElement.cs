using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibElement
    {
        private bool _isSetup;

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

        public bool IsSpawned
        {
            get { return _isSetup && Identity.IsSpawned; }
        }

        public bool IsDestroyed
        {
            get { return _isSetup && Identity.IsDestroyed; }
        }

        public long ConnectionId
        {
            get { return !_isSetup ? -1 : Identity.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return !_isSetup ? 0 : Identity.ObjectId; }
        }

        public byte SyncChannelId
        {
            get { return !_isSetup ? (byte)0 : Identity.SyncChannelId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return !_isSetup ? null : Identity.Manager; }
        }

        public LiteNetLibPlayer Player
        {
            get { return !_isSetup ? null : Identity.Player; }
        }

        public bool IsServer
        {
            get { return _isSetup && Identity.IsServer; }
        }

        public bool IsClient
        {
            get { return _isSetup && Identity.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return _isSetup && Identity.IsOwnerClient; }
        }

        public bool IsOwnerHost
        {
            get { return _isSetup && Identity.IsOwnerHost; }
        }

        public bool IsOwnedByServer
        {
            get { return _isSetup && Identity.IsOwnedByServer; }
        }

        public bool IsOwnerClientOrOwnedByServer
        {
            get { return _isSetup && Identity.IsOwnerClientOrOwnedByServer; }
        }

        public bool IsSceneObject
        {
            get { return _isSetup && Identity.IsSceneObject; }
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
                return (!_isSetup ? LiteNetLibBehaviour.TAG_NULL : Behaviour.LogTag) + ".E";
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
            _isSetup = true;
        }
    }
}
