namespace LiteNetLibManager
{
    public enum LiteNetLibSyncFieldMode : byte
    {
        /// <summary>
        /// Changes handle by server
        /// Will send to connected clients when changes occurs on server
        /// </summary>
        ServerToClients,
        /// <summary>
        /// Changes handle by server
        /// Will send to owner-client when changes occurs on server
        /// </summary>
        ServerToOwnerClient,
        /// <summary>
        /// Changes handle by owner-client
        /// Will send to server then server multicast to other clients when changes occurs on owner-client
        /// </summary>
        ClientMulticast
    }
}