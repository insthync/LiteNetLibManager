using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibSyncingStates
    {
        private readonly Dictionary<byte, Dictionary<uint, GameStateSyncData>> _states = new Dictionary<byte, Dictionary<uint, GameStateSyncData>>();
        public Dictionary<byte, Dictionary<uint, GameStateSyncData>> States => _states;

        public void Clear()
        {
            _states.Clear();
        }

        public Dictionary<uint, GameStateSyncData> PrepareSyncStateCollection(byte channelId)
        {
            if (!_states.TryGetValue(channelId, out var collectionByObjectId))
            {
                collectionByObjectId = new Dictionary<uint, GameStateSyncData>();
                _states[channelId] = collectionByObjectId;
            }
            return collectionByObjectId;
        }

        public GameStateSyncData PrepareSyncStateData(byte channelId, uint objectId)
        {
            var collectionByObjectId = PrepareSyncStateCollection(channelId);
            if (!collectionByObjectId.TryGetValue(objectId, out var syncData))
            {
                syncData = new GameStateSyncData();
                collectionByObjectId[objectId] = syncData;
            }
            return syncData;
        }

        public void AppendSpawnSyncState(LiteNetLibIdentity identity)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            syncData.Identity = identity;
            syncData.StateType = GameStateSyncType.Spawn;
            syncData.DestroyReasons = 0;
            syncData.SyncElements.Clear();
        }

        public void AppendDestroySyncState(LiteNetLibIdentity identity, byte reasons)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            syncData.Identity = identity;
            syncData.StateType = GameStateSyncType.Destroy;
            syncData.DestroyReasons = reasons;
            syncData.SyncElements.Clear();
        }

        public void AppendDataSyncState(LiteNetLibSyncElement syncElement)
        {
            if (syncElement.Identity == null)
            {
                Logging.LogError("Unable to append base-line data sync state, sync element's identity is null");
                return;
            }
            byte channelId = syncElement.SyncChannelId;
            uint objectId = syncElement.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            if (syncData.StateType == GameStateSyncType.Spawn || syncData.StateType == GameStateSyncType.Destroy)
            {
                // Unable to sync data, it is spawning or destroying
                return;
            }
            syncData.Identity = syncElement.Identity;
            syncData.StateType = GameStateSyncType.Data;
            syncData.DestroyReasons = 0;
            syncData.SyncElements.Add(syncElement);
        }

        public void RemoveSyncState(LiteNetLibIdentity identity)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var collectionByObjectId = PrepareSyncStateCollection(channelId);
            collectionByObjectId.Remove(objectId);
        }
    }
}
