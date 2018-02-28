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
        internal bool IsReady { get; set; }
        internal readonly HashSet<LiteNetLibIdentity> SubscribingObjects = new HashSet<LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, NetPeer peer)
        {
            Manager = manager;
            Peer = peer;
        }

        internal void AddSubscribing(LiteNetLibIdentity identity)
        {
            SubscribingObjects.Add(identity);

            Manager.SendServerSpawnObjectWithData(Peer, identity);
        }

        internal void RemoveSubscribing(LiteNetLibIdentity identity, bool destroyObjectsOnPeer)
        {
            SubscribingObjects.Remove(identity);

            if (destroyObjectsOnPeer)
                Manager.SendServerDestroyObject(Peer, identity.ObjectId);
        }

        internal void ClearSubscribing()
        {
            // Remove this from identities subscriber list
            foreach (var identity in SubscribingObjects)
            {
                // Don't call for remove subscribing 
                // because it's going to clear in this function
                identity.RemoveSubscriber(this, false);
            }
            SubscribingObjects.Clear();
        }

        /// <summary>
        /// Call this function to destroy all objects that spawned by this player
        /// </summary>
        internal void DestroyAllObjects()
        {
            var objectIds = new List<uint>(SpawnedObjects.Keys);
            foreach (var objectId in objectIds)
                Manager.Assets.NetworkDestroy(objectId);
        }
    }
}
