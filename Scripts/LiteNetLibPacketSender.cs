using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public static class LiteNetLibPacketSender
    {
        public static readonly NetDataWriter Writer = new NetDataWriter();

        public static void SendPacket(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType, System.Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.Put(msgType);
            if (serializer != null)
                serializer(writer);
            peer.Send(writer, options);
        }

        public static void SendPacket(SendOptions options, NetPeer peer, short msgType, System.Action<NetDataWriter> serializer)
        {
            SendPacket(Writer, options, peer, msgType, serializer);
        }

        public static void SendPacket<T>(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType, T messageData) where T : ILiteNetLibMessage
        {
            SendPacket(writer, options, peer, msgType, messageData.Serialize);
        }

        public static void SendPacket<T>(SendOptions options, NetPeer peer, short msgType, T messageData) where T : ILiteNetLibMessage
        {
            SendPacket(Writer, options, peer, msgType, messageData);
        }

        public static void SendPacket(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType)
        {
            SendPacket(writer, options, peer, msgType, null);
        }

        public static void SendPacket(SendOptions options, NetPeer peer, short msgType)
        {
            SendPacket(Writer, options, peer, msgType);
        }
    }
}
