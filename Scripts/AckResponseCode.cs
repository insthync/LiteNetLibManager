namespace LiteNetLibManager
{
    public enum AckResponseCode : byte
    {
        Default = 0,
        Success = 1,
        Timeout = 2,
        Error = 3,
        Unimplemented = 4,
    }
}
