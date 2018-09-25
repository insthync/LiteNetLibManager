using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ITransport
    {
        bool IsClientConnected();
        void ClientConnect(string connectKey, string address, int port);
        bool ClientSend(SendOptions sendOptions, NetDataWriter writer);
        ENetworkEvent ClientReceive(out NetDataReader reader, out DisconnectInfo disconnectInfo);
        void ClientDisconnect();
        bool IsServerActive();
        void ServerStart(string connectKey, int port, int maxConnections);
        bool ServerSend(long connectionId, SendOptions sendOptions, NetDataWriter writer);
        ENetworkEvent ServerReceive(out long connectionId, out NetDataReader reader, out DisconnectInfo disconnectInfo);
        bool ServerDisconnect(long connectionId);
        void ServerStop();
        void Destroy();
    }
}
