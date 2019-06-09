using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ITransport
    {
        bool IsClientStarted();
        bool StartClient(string address, int port);
        bool ClientSend(DeliveryMethod deliveryMethod, NetDataWriter writer);
        bool ClientReceive(out TransportEventData eventData);
        void StopClient();
        bool IsServerStarted();
        bool StartServer(int port, int maxConnections);
        bool ServerSend(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer);
        bool ServerReceive(out TransportEventData eventData);
        bool ServerDisconnect(long connectionId);
        void StopServer();
        void Destroy();
        int GetServerPeersCount();
    }
}
