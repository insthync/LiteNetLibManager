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
        internal readonly Dictionary<byte, Dictionary<uint, GameStateSyncData>> SyncingStates = new Dictionary<byte, Dictionary<uint, GameStateSyncData>>();

        public LiteNetLibPlayer(LiteNetLibGameManager manager, long connectionId)
        {
            Manager = manager;
            ConnectionId = connectionId;
            RttCalculator = new RttCalculator();
        }

        internal Dictionary<uint, GameStateSyncData> PrepareSyncStateCollection(byte channelId)
        {
            if (!SyncingStates.TryGetValue(channelId, out var collectionByObjectId))
            {
                collectionByObjectId = new Dictionary<uint, GameStateSyncData>();
                SyncingStates[channelId] = collectionByObjectId;
            }
            return collectionByObjectId;
        }

        internal GameStateSyncData PrepareSyncStateData(byte channelId, uint objectId)
        {
            var collectionByObjectId = PrepareSyncStateCollection(channelId);
            if (!collectionByObjectId.TryGetValue(objectId, out var syncData))
            {
                syncData = new GameStateSyncData();
                collectionByObjectId[objectId] = syncData;
            }
            return syncData;
        }

        internal void AppendSpawnSyncState(LiteNetLibIdentity identity)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            syncData.StateType = GameStateSyncData.STATE_TYPE_SPAWN;
            syncData.SyncElements.Clear();
        }

        internal void AppendDataSyncState(LiteNetLibSyncElement syncElement)
        {
            byte channelId = syncElement.SyncChannelId;
            uint objectId = syncElement.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            if (syncData.StateType != GameStateSyncData.STATE_TYPE_NONE && syncData.StateType != GameStateSyncData.STATE_TYPE_SYNC)
                return;
            syncData.StateType = GameStateSyncData.STATE_TYPE_SYNC;
            syncData.SyncElements.Add(syncElement);
        }

        internal void AppendDestroySyncState(LiteNetLibIdentity identity, byte reasons)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            syncData.StateType = GameStateSyncData.STATE_TYPE_DESTROY;
            syncData.SyncElements.Clear();
            syncData.DestroyReasons = reasons;
        }

        internal void RemoveSyncState(LiteNetLibIdentity identity)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var collectionByObjectId = PrepareSyncStateCollection(channelId);
            collectionByObjectId.Remove(objectId);
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
                AppendSpawnSyncState(identity);
            }
        }

        internal void Unsubscribe(uint objectId)
        {
            LiteNetLibIdentity identity;
            if (Subscribings.Remove(objectId) && Manager.Assets.TryGetSpawnedObject(objectId, out identity))
            {
                identity.RemoveSubscriber(ConnectionId);
                AppendDestroySyncState(identity, DestroyObjectReasons.RemovedFromSubscribing);
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
                    AppendDestroySyncState(identity, DestroyObjectReasons.RemovedFromSubscribing);
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
