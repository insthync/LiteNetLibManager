using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibSyncingStates
    {
        private const int MaxCachedStates = 1024;
        private const int MaxCachedChannels = 32;
        private readonly Dictionary<byte, Dictionary<uint, GameStateSyncData>> _states = new Dictionary<byte, Dictionary<uint, GameStateSyncData>>();
        private readonly Stack<GameStateSyncData> _cachedStates = new Stack<GameStateSyncData>();
        private readonly Stack<Dictionary<uint, GameStateSyncData>> _cachedChannels = new Stack<Dictionary<uint, GameStateSyncData>>();
        public Dictionary<byte, Dictionary<uint, GameStateSyncData>> States => _states;

        public void Clear()
        {
            foreach (var collection in _states.Values)
                RecycleCollection(collection);
            _states.Clear();
        }

        public void ClearChannel(byte channelId)
        {
            if (!_states.TryGetValue(channelId, out var collection))
                return;
            foreach (var state in collection.Values)
                RecycleState(state);
            collection.Clear();
        }

        private void RecycleState(GameStateSyncData state)
        {
            state.Reset();
            if (_cachedStates.Count < MaxCachedStates)
                _cachedStates.Push(state);
        }

        private void RecycleCollection(Dictionary<uint, GameStateSyncData> collection)
        {
            foreach (var state in collection.Values)
                RecycleState(state);
            collection.Clear();
            if (_cachedChannels.Count < MaxCachedChannels)
                _cachedChannels.Push(collection);
        }

        public Dictionary<uint, GameStateSyncData> PrepareSyncStateCollection(byte channelId)
        {
            if (!_states.TryGetValue(channelId, out var collectionByObjectId))
            {
                collectionByObjectId = _cachedChannels.Count > 0
                    ? _cachedChannels.Pop()
                    : new Dictionary<uint, GameStateSyncData>();
                _states[channelId] = collectionByObjectId;
            }
            return collectionByObjectId;
        }

        public GameStateSyncData PrepareSyncStateData(byte channelId, uint objectId)
        {
            var collectionByObjectId = PrepareSyncStateCollection(channelId);
            if (!collectionByObjectId.TryGetValue(objectId, out var syncData))
            {
                syncData = _cachedStates.Count > 0 ? _cachedStates.Pop() : new GameStateSyncData();
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
            if (!_states.TryGetValue(channelId, out var collectionByObjectId) ||
                !collectionByObjectId.TryGetValue(objectId, out var state))
                return;
            collectionByObjectId.Remove(objectId);
            RecycleState(state);
            if (collectionByObjectId.Count == 0)
            {
                _states.Remove(channelId);
                RecycleCollection(collectionByObjectId);
            }
        }
    }
}
