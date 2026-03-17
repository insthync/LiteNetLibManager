using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class GameStateSyncData
    {
        public LiteNetLibIdentity Identity;
        public GameStateSyncType StateType = GameStateSyncType.None;
        public byte DestroyReasons = 0;
        public readonly HashSet<LiteNetLibSyncElement> SyncElements = new HashSet<LiteNetLibSyncElement>();

        public void Reset()
        {
            Identity = null;
            StateType = GameStateSyncType.None;
            DestroyReasons = 0;
            SyncElements.Clear();
        }
    }
}
