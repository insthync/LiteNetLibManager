using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibPlayer
    {
        public LiteNetLibGameManager Manager { get; protected set; }
        public long ConnectionId { get; protected set; }
        public RttCalculator RttCalculator { get; protected set; }
        public long Rtt { get => RttCalculator.Rtt; }
        public bool IsReady { get; set; }

        internal readonly HashSet<uint> Subscribings = new HashSet<uint>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        internal readonly Dictionary<byte, HashSet<uint>> NetworkSpawningObjectIds = new Dictionary<byte, HashSet<uint>>();
        internal readonly Dictionary<byte, HashSet<DestroyObjectInfo>> NetworkDestroyingObjectIds = new Dictionary<byte, HashSet<DestroyObjectInfo>>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, long connectionId)
        {
            Manager = manager;
            ConnectionId = connectionId;
            RttCalculator = new RttCalculator();
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
                NetworkDestroyingObjectIds.Remove(objectId);
                NetworkSpawningObjectIds.Add(objectId);
            }
        }

        internal void Unsubscribe(uint objectId)
        {
            LiteNetLibIdentity identity;
            if (Subscribings.Remove(objectId) && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
            {
                identity.RemoveSubscriber(ConnectionId);
                NetworkSpawningObjectIds.Remove(objectId);
                NetworkDestroyingObjectIds[objectId] = DestroyObjectReasons.RemovedFromSubscribing;
            }
        }

        internal void ClearSubscribing(bool destroyObjectsOnPeer)
        {
            // Remove player's subscribing and remove this player from subscribers
            LiteNetLibIdentity identity;
            foreach (uint objectId in Subscribings)
            {
                if (!Manager.Assets.TryGetSpawnedObject(objectId, out identity))
                    continue;
                identity.RemoveSubscriber(ConnectionId);
                if (destroyObjectsOnPeer)
                {
                    NetworkSpawningObjectIds.Remove(objectId);
                    NetworkDestroyingObjectIds[objectId] = DestroyObjectReasons.RemovedFromSubscribing;
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
                {
                    SpawnedObjects[objectId].ConnectionId = -1;
                    continue;
                }
                Manager.Assets.NetworkDestroy(objectId, DestroyObjectReasons.RequestedToDestroy);
            }
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
