namespace LiteNetLibManager
{
    public static partial class GameMsgTypes
    {
        public const ushort Request = 0;
        public const ushort Response = 1;
        public const ushort CallFunction = 2;
        public const ushort SyncStates = 3;
        public const ushort SyncElement = 4;
        public const ushort ServerError = 5;
        public const ushort ServerSceneChange = 6;
        public const ushort ServerSetObjectOwner = 7;
        public const ushort Ping = 8;
        public const ushort Pong = 9;
        public const ushort Disconnect = 10;
        public const ushort Highest = 10;
    }
}
