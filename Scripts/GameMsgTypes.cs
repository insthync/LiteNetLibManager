namespace LiteNetLibManager
{
    public static partial class GameMsgTypes
    {
        public const ushort Request = 0;
        public const ushort Response = 1;
        public const ushort CallFunction = 3;
        public const ushort ServerSpawnSceneObject = 4;
        public const ushort ServerSpawnObject = 5;
        public const ushort ServerDestroyObject = 6;
        public const ushort UpdateSyncField = 7;
        public const ushort InitialSyncField = 8;
        public const ushort OperateSyncList = 9;
        public const ushort ServerSyncBehaviour = 11;
        public const ushort ServerError = 12;
        public const ushort ServerSceneChange = 13;
        public const ushort ClientSendTransform = 14;
        public const ushort ServerSetObjectOwner = 15;
        public const ushort Ping = 16;
        public const ushort Pong = 17;
        public const ushort Disconnect = 18;
        public const ushort Highest = 18;
    }
}
