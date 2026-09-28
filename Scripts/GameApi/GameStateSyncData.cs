using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class GameStateSyncData
    {
        public LiteNetLibIdentity Identity;
        public GameStateSyncType StateType = GameStateSyncType.None;
        public byte DestroyReasons = 0;
        public readonly HashSet<LiteNetLibSyncElement> SyncElements = new HashSet<LiteNetLibSyncElement>();
        private HashSet<LiteNetLibSyncList> _fullListSyncElements;

        internal void MarkFullListSync(LiteNetLibSyncList syncList)
        {
            if (_fullListSyncElements == null)
                _fullListSyncElements = new HashSet<LiteNetLibSyncList>();
            _fullListSyncElements.Add(syncList);
        }

        internal bool ShouldSyncFullList(LiteNetLibSyncList syncList)
        {
            return _fullListSyncElements != null && _fullListSyncElements.Contains(syncList);
        }

        internal void RemoveFullListSync(LiteNetLibSyncList syncList)
        {
            if (_fullListSyncElements != null)
                _fullListSyncElements.Remove(syncList);
        }

        internal void ResetFullListSync()
        {
            if (_fullListSyncElements != null)
                _fullListSyncElements.Clear();
        }

        public void Reset()
        {
            Identity = null;
            StateType = GameStateSyncType.None;
            DestroyReasons = 0;
            SyncElements.Clear();
            ResetFullListSync();
        }
    }
}
