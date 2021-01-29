using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public abstract class BaseLiteNetLibVisibleChecker : MonoBehaviour
    {
        private bool isFoundIdentity;
        private LiteNetLibIdentity identity;
        public LiteNetLibIdentity Identity
        {
            get
            {
                if (!isFoundIdentity)
                {
                    identity = GetComponent<LiteNetLibIdentity>();
                    if (identity == null)
                        identity = GetComponentInParent<LiteNetLibIdentity>();
                    isFoundIdentity = identity != null;
                }
                return identity;
            }
        }

        public long ConnectionId
        {
            get { return Identity.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return Identity.ObjectId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return Identity.Manager; }
        }

        public bool IsServer
        {
            get { return Identity.IsServer; }
        }

        public bool IsClient
        {
            get { return Identity.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return Identity.IsOwnerClient; }
        }

        public bool IsSceneObject
        {
            get { return Identity.IsSceneObject; }
        }

        public abstract bool ShouldSubscribe(LiteNetLibIdentity identity);

        public abstract HashSet<uint> GetInitializeSubscribings();

        public void UpdateSubscribings(HashSet<uint> objectIds)
        {
            Identity.UpdateSubscribings(objectIds);
        }
    }
}
