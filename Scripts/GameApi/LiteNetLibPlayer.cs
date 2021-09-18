using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public long ConnectionId { get; protected set; }
        public bool IsReady { get; set; }
        public long Rtt { get; internal set; }
        internal long LastPingTime { get; set; }
        internal readonly HashSet<uint> Subscribings = new HashSet<uint>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, long connectionId)
        {
            Manager = manager;
            ConnectionId = connectionId;
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

        internal void Unsubscribe(uint objectId)
        {
            LiteNetLibIdentity identity;
            if (Subscribings.Remove(objectId) && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
            {
                identity.RemoveSubscriber(ConnectionId);
                Manager.SendServerDestroyObject(ConnectionId, objectId, DestroyObjectReasons.RemovedFromSubscribing);
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

        internal void DestroyObjectsWhenNotReady()
        {
            List<uint> objectIds = new List<uint>(SpawnedObjects.Keys);
            foreach (uint objectId in objectIds)
                Manager.Assets.NetworkDestroy(objectId, DestroyObjectReasons.RequestedToDestroy);
            SpawnedObjects.Clear();
        }

        internal void DestroyObjectsWhenDisconnect()
        {
            List<uint> objectIds = new List<uint>(SpawnedObjects.Keys);
            foreach (uint objectId in objectIds)
            {
                if (SpawnedObjects[objectId].DoNotDestroyWhenDisconnect)
                    continue;
                Manager.Assets.NetworkDestroy(objectId, DestroyObjectReasons.RequestedToDestroy);
                SpawnedObjects.Remove(objectId);
            }
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

        public IEnumerable<LiteNetLibIdentity> GetSpawnedObjects()
        {
            return SpawnedObjects.Values;
        }

        public IEnumerable<uint> GetSubscribingObjectIds()
        {
            return Subscribings;
        }

        public int SpawnedObjectsCount
        {
            get { return SpawnedObjects.Count; }
        }
    }
}
