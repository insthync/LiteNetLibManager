namespace LiteNetLibManager
{
    /// <summary>
    /// https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/code
    /// </summary>
    public enum WebSocketCloseCode : ushort
    {
        NormalClosure = 1000,
        EndpointUnavailable = 1001,
        ProtocolError = 1002,
        InvalidMessageType = 1003,
        Empty = 1005,
        AbnormalClosure = 1006,
        InvalidPayloadData = 1007,
        PolicyViolation = 1008,
        MessageTooBig = 1009,
        MandatoryExtension = 1010,
        InternalServerError = 1011,
        ServiceRestart = 1012,
        TryAgainLater = 1013,
        BadGateway = 1014,
        TlsHandshakeFailure = 1015
    }
}