using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibSyncingDeltaStates
    {
        private readonly Dictionary<uint, GameStateSyncData> _states = new Dictionary<uint, GameStateSyncData>();
        public Dictionary<uint, GameStateSyncData> States => _states;

        public void Clear()
        {
            _states.Clear();
        }

        public void AppendDataSyncState(LiteNetLibSyncElement syncElement)
        {
            if (syncElement.Identity == null)
            {
                Logging.LogError("Unable to append data sync state, sync element's identity is null");
                return;
            }
            uint objectId = syncElement.ObjectId;
            if (!_states.TryGetValue(objectId, out var syncData))
            {
                syncData = new GameStateSyncData();
                _states[objectId] = syncData;
            }
            syncData.Identity = syncElement.Identity;
            syncData.StateType = GameStateSyncType.Data;
            syncData.SyncElements.Add(syncElement);
        }

        public void RemoveSyncState(LiteNetLibIdentity identity)
        {
            uint objectId = identity.ObjectId;
            _states.Remove(objectId);
        }
    }
}
