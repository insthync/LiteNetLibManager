using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public long ConnectionId { get; protected set; }

        public bool IsReady { get; set; }
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
        }

        internal void RemoveSubscribing(LiteNetLibIdentity identity, bool destroyObjectsOnPeer)
        {
            SubscribingObjects.Remove(identity);

            if (destroyObjectsOnPeer)
                Manager.SendServerDestroyObject(ConnectionId, identity.ObjectId, LiteNetLibGameManager.DestroyObjectReasons.RemovedFromSubscribing);
        }

        internal void ClearSubscribing(bool destroyObjectsOnPeer)
        {
            // Remove this from identities subscriber list
            foreach (LiteNetLibIdentity identity in SubscribingObjects)
            {
                // Don't call for remove subscribing 
                // because it's going to clear in this function
                identity.RemoveSubscriber(this, false);
                if (destroyObjectsOnPeer)
                    Manager.SendServerDestroyObject(ConnectionId, identity.ObjectId, LiteNetLibGameManager.DestroyObjectReasons.RemovedFromSubscribing);
            }
            SubscribingObjects.Clear();
        }

        /// <summary>
        /// Call this function to destroy all objects that spawned by this player
        /// </summary>
        internal void DestroyAllObjects()
        {
            foreach (uint objectId in SpawnedObjects.Keys)
                Manager.Assets.NetworkDestroy(objectId, LiteNetLibGameManager.DestroyObjectReasons.RequestedToDestroy);
            SpawnedObjects.Clear();
        }

        public bool TryGetSpawnedObject(uint objectId, out LiteNetLibIdentity identity)
        {
            return SpawnedObjects.TryGetValue(objectId, out identity);
        }

        public bool ContainsSpawnedObject(uint objectId)
        {
            return SpawnedObjects.ContainsKey(objectId);
        }

        public LiteNetLibIdentity GetSpawnedObject(uint objectId)
        {
            return SpawnedObjects[objectId];
        }

        public Dictionary<uint, LiteNetLibIdentity>.ValueCollection GetSpawnedObjects()
        {
            return SpawnedObjects.Values;
        }

        public int SpawnedObjectsCount
        {
            get { return SpawnedObjects.Count; }
        }
    }
}
