using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibIdentity))]
    public class LiteNetLibBehaviour : MonoBehaviour
    {
        private LiteNetLibIdentity identity;
        public LiteNetLibIdentity Identity
        {
            get
            {
                if (identity == null)
                    identity = GetComponent<LiteNetLibIdentity>();
                return identity;
            }
        }

        public long ConnectId
        {
            get { return Identity.ConnectId; }
        }

        public uint ObjectId
        {
            get { return Identity.ObjectId; }
        }

        public LiteNetLibManager Manager
        {
            get { return Identity.Manager; }
        }

        public bool IsServer
        {
            get { return Manager.IsServer; }
        }

        public bool IsClient
        {
            get { return Manager.IsClient; }
        }
        
        public bool IsLocalClient
        {
            get { return ConnectId == Manager.Client.Peer.ConnectId; }
        }
    }
}
