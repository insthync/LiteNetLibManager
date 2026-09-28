using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibSyncingDeltaStates
    {
        private const int MaxCachedStates = 1024;
        private readonly Dictionary<uint, GameStateSyncData> _states = new Dictionary<uint, GameStateSyncData>();
        private readonly Stack<GameStateSyncData> _cachedStates = new Stack<GameStateSyncData>();
        public Dictionary<uint, GameStateSyncData> States => _states;

        public void Clear()
        {
            foreach (var state in _states.Values)
                RecycleState(state);
            _states.Clear();
        }

        private void RecycleState(GameStateSyncData state)
        {
            state.Reset();
            if (_cachedStates.Count < MaxCachedStates)
                _cachedStates.Push(state);
        }

        public void AppendDataSyncState(LiteNetLibSyncElement syncElement)
        {
            if (syncElement.Identity == null)
            {
                Logging.LogError("Unable to append delta data sync state, sync element's identity is null");
                return;
            }
            uint objectId = syncElement.ObjectId;
            if (!_states.TryGetValue(objectId, out var syncData))
            {
                syncData = _cachedStates.Count > 0 ? _cachedStates.Pop() : new GameStateSyncData();
                _states[objectId] = syncData;
            }
            syncData.Identity = syncElement.Identity;
            syncData.StateType = GameStateSyncType.Data;
            syncData.SyncElements.Add(syncElement);
        }

        public void RemoveSyncState(LiteNetLibIdentity identity)
        {
            uint objectId = identity.ObjectId;
            if (_states.TryGetValue(objectId, out var state))
            {
                _states.Remove(objectId);
                RecycleState(state);
            }
        }
    }
}
