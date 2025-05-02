namespace LiteNetLibManager
{
    public static partial class GameMsgTypes
    {
        public const ushort Request = 0;
        public const ushort Response = 1;
        public const ushort CallFunction = 2;
        public const ushort ServerSpawnObjects = 3;
        public const ushort ServerDestroyObjects = 4;
        public const ushort SyncElements = 5;
        public const ushort ServerError = 6;
        public const ushort ServerSceneChange = 7;
        public const ushort ServerSetObjectOwner = 8;
        public const ushort Ping = 9;
        public const ushort Pong = 10;
        public const ushort Disconnect = 11;
        public const ushort Highest = 11;
    }
}
