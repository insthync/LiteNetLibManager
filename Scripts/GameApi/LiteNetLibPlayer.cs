using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public long ConnectionId { get; protected set; }

        public bool IsReady { get; set; }
        internal readonly HashSet<uint> Subscribings = new HashSet<uint>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, long connectionId)
        {
            Manager = manager;
            ConnectionId = connectionId;
        }

        internal void NotifyNewObject(LiteNetLibIdentity identity)
        {
            foreach (LiteNetLibIdentity obj in GetSpawnedObjects())
            {
                obj.NotifyNewObject(identity);
            }
        }

        internal bool IsSubscribing(uint objectId)
        {
            return Subscribings.Contains(objectId);
        }

        internal int CountSubscribing()
        {
            return Subscribings.Count;
        }

        internal void Subscribe(uint objectId)
        {
            LiteNetLibIdentity identity;
            if (Subscribings.Add(objectId) && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
            {
                identity.AddSubscriber(ConnectionId);
                Manager.SendServerSpawnObjectWithData(ConnectionId, identity);
            }
        }

        internal void Unsubscribe(uint objectId, bool destroyObjectOnPeer)
        {
            if (Subscribings.Remove(objectId))
            {
                LiteNetLibIdentity identity;
                if (destroyObjectOnPeer && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
                {
                    identity.RemoveSubscriber(ConnectionId);
                    Manager.SendServerDestroyObject(ConnectionId, objectId, DestroyObjectReasons.RemovedFromSubscribing);
                }
            }
        }

        internal void ClearSubscribing(bool destroyObjectsOnPeer)
        {
            // Remove this from identities subscriber list
            LiteNetLibIdentity identity;
            foreach (uint objectId in Subscribings)
            {
                // Don't call for remove subscribing 
                // because it's going to clear in this function
                if (destroyObjectsOnPeer && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
                {
                    identity.RemoveSubscriber(ConnectionId);
                    Manager.SendServerDestroyObject(ConnectionId, objectId, DestroyObjectReasons.RemovedFromSubscribing);
                }
            }
            Subscribings.Clear();
        }

        /// <summary>
        /// Call this function to destroy all objects that spawned by this player
        /// </summary>
        internal void DestroyAllObjects()
        {
            List<uint> objectIds = new List<uint>(SpawnedObjects.Keys);
            foreach (uint objectId in objectIds)
                Manager.Assets.NetworkDestroy(objectId, DestroyObjectReasons.RequestedToDestroy);
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
