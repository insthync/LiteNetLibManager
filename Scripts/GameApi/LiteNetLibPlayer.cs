using System.Collections;
using System.Collections.Generic;
using LiteNetLib;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public NetPeer Peer { get; protected set; }
        public long ConnectId { get { return Peer.ConnectId; } }
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, NetPeer peer)
        {
            Manager = manager;
            Peer = peer;
        }

        internal void DestoryAllObjects()
        {
            var objectIds = new List<uint>(SpawnedObjects.Keys);
            foreach (var objectId in objectIds)
                Manager.Assets.NetworkDestroy(objectId);
        }
    }
}
