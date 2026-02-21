using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class GameStateSyncData
    {
        public LiteNetLibIdentity Identity;
        public GameStateSyncType StateType = GameStateSyncType.None;
        public byte DestroyReasons = 0;
        public readonly HashSet<LiteNetLibSyncElement> SyncBaseLineElements = new HashSet<LiteNetLibSyncElement>();
        public readonly HashSet<LiteNetLibSyncElement> SyncDeltaElements = new HashSet<LiteNetLibSyncElement>();

        public void Reset()
        {
            Identity = null;
            StateType = GameStateSyncType.None;
            DestroyReasons = 0;
            SyncBaseLineElements.Clear();
            SyncDeltaElements.Clear();
        }
    }
}
