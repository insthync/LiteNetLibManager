namespace LiteNetLibManager
{
    public static partial class GameMsgTypes
    {
        public const ushort Request = 0;
        public const ushort Response = 1;
        public const ushort CallFunction = 2;
        public const ushort SyncElements = 3;
        public const ushort ServerError = 4;
        public const ushort ServerSceneChange = 5;
        public const ushort ServerSetObjectOwner = 6;
        public const ushort Ping = 7;
        public const ushort Pong = 8;
        public const ushort Disconnect = 9;
        public const ushort Highest = 9;
    }
}
