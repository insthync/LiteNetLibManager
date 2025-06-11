using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibSyncingStates
    {
        private readonly Dictionary<byte, Dictionary<uint, GameStateSyncData>> _states = new Dictionary<byte, Dictionary<uint, GameStateSyncData>>();
        public IReadOnlyDictionary<byte, Dictionary<uint, GameStateSyncData>> States => _states;

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
            syncData.StateType = GameStateSyncData.STATE_TYPE_SPAWN;
            syncData.SyncElements.Clear();
        }

        public void AppendDataSyncState(LiteNetLibSyncElement syncElement)
        {
            byte channelId = syncElement.SyncChannelId;
            uint objectId = syncElement.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            if (syncData.StateType != GameStateSyncData.STATE_TYPE_NONE && syncData.StateType != GameStateSyncData.STATE_TYPE_SYNC)
                return;
            syncData.StateType = GameStateSyncData.STATE_TYPE_SYNC;
            syncData.SyncElements.Add(syncElement);
        }

        public void AppendDestroySyncState(LiteNetLibIdentity identity, byte reasons)
        {
            byte channelId = identity.SyncChannelId;
            uint objectId = identity.ObjectId;
            var syncData = PrepareSyncStateData(channelId, objectId);
            syncData.StateType = GameStateSyncData.STATE_TYPE_DESTROY;
            syncData.SyncElements.Clear();
            syncData.DestroyReasons = reasons;
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
