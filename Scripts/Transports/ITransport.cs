using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ITransport
    {
        int ServerPeersCount { get; }
        int ServerMaxConnections { get; }
        bool IsClientStarted { get; }
        bool IsServerStarted { get; }
        bool StartClient(string address, int port);
        bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer);
        bool ClientReceive(out TransportEventData eventData);
        void StopClient();
        bool StartServer(int port, int maxConnections);
        bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer);
        bool ServerReceive(out TransportEventData eventData);
        bool ServerDisconnect(long connectionId);
        void StopServer();
        void Destroy();
    }
}
