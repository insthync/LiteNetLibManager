using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class GameStateSyncData
    {
        public const byte STATE_TYPE_SPAWN = 1;
        public const byte STATE_TYPE_SYNC = 2;
        public const byte STATE_TYPE_DESTROY = 3;

        public byte StateType = 0;
        public byte DestroyReasons = 0;
        public readonly HashSet<LiteNetLibSyncElement> SyncElements = new HashSet<LiteNetLibSyncElement>();
    }
}