using System.Collections;
using System.Collections.Generic;
using LiteNetLib;

namespace LiteNetLibManager
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public long ConnectionId { get; protected set; }

        public bool IsOwnerClient
        {
            get { return ConnectionId == Manager.Client.ClientConnectionId; }
        }

        internal bool IsReady { get; set; }
        internal readonly HashSet<LiteNetLibIdentity> SubscribingObjects = new HashSet<LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, long connectionId)
        {
            Manager = manager;
            ConnectionId = connectionId;
        }

        internal void AddSubscribing(LiteNetLibIdentity identity)
        {
            SubscribingObjects.Add(identity);

            Manager.SendServerSpawnObjectWithData(ConnectionId, identity);
            // If this is player for local host client, show object
            if (Manager.IsServer && Manager.IsClient && IsOwnerClient)
                identity.OnServerSubscribingAdded();
        }

        internal void RemoveSubscribing(LiteNetLibIdentity identity, bool destroyObjectsOnPeer)
        {
            SubscribingObjects.Remove(identity);

            if (destroyObjectsOnPeer)
            {
                Manager.SendServerDestroyObject(ConnectionId, identity.ObjectId, DestroyObjectReasons.RemovedFromSubscribing);
                // If this is player for local host client, hide object
                if (Manager.IsServer && Manager.IsClient && IsOwnerClient)
                    identity.OnServerSubscribingRemoved();
            }
        }

        internal void ClearSubscribing(bool destroyObjectsOnPeer)
        {
            // Remove this from identities subscriber list
            foreach (var identity in SubscribingObjects)
            {
                // Don't call for remove subscribing 
                // because it's going to clear in this function
                identity.RemoveSubscriber(this, false);
                if (destroyObjectsOnPeer)
                    Manager.SendServerDestroyObject(ConnectionId, identity.ObjectId, DestroyObjectReasons.RemovedFromSubscribing);
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
                Manager.Assets.NetworkDestroy(objectId, DestroyObjectReasons.RequestedToDestroy);
            SpawnedObjects.Clear();
        }
    }
}
