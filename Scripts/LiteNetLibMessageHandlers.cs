using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibMessageHandlers : MonoBehaviour
    {
        protected readonly Dictionary<short, MessageHandlerDelegate> serverMessageHandlers = new Dictionary<short, MessageHandlerDelegate>();
        protected readonly Dictionary<short, MessageHandlerDelegate> clientMessageHandlers = new Dictionary<short, MessageHandlerDelegate>();

        private LiteNetLibManager manager;
        public LiteNetLibManager Manager
        {
            get
            {
                if (manager == null)
                    manager = GetComponent<LiteNetLibManager>();
                return manager;
            }
        }

        public readonly NetDataWriter Writer = new NetDataWriter();
        
        public void ServerReadPacket(NetPeer peer, NetDataReader reader)
        {
            ReadPacket(peer, reader, serverMessageHandlers);
        }

        public void ClientReadPacket(NetPeer peer, NetDataReader reader)
        {
            ReadPacket(peer, reader, clientMessageHandlers);
        }

        public void SendPacket(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType, System.Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.Put(msgType);
            serializer(writer);
            peer.Send(writer, options);
        }

        public void SendPacket(SendOptions options, NetPeer peer, short msgType, System.Action<NetDataWriter> serializer)
        {
            SendPacket(Writer, options, peer, msgType, serializer);
        }

        public void SendPacket<T>(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType, T messageData) where T : ILiteNetLibMessage
        {
            SendPacket(writer, options, peer, msgType, messageData.Serialize);
        }

        public void SendPacket<T>(SendOptions options, NetPeer peer, short msgType, T messageData) where T : ILiteNetLibMessage
        {
            SendPacket(Writer, options, peer, msgType, messageData);
        }

        public void SendPacket(NetDataWriter writer, SendOptions options, NetPeer peer, short msgType)
        {
            writer.Reset();
            writer.Put(msgType);
            peer.Send(writer, options);
        }

        public void SendPacket(SendOptions options, NetPeer peer, short msgType)
        {
            SendPacket(Writer, options, peer, msgType);
        }

        private void ReadPacket(NetPeer peer, NetDataReader reader, Dictionary<short, MessageHandlerDelegate> registerDict)
        {
            var msgType = reader.GetShort();
            MessageHandlerDelegate handlerDelegate;
            if (registerDict.TryGetValue(msgType, out handlerDelegate))
            {
                var messageHandler = new LiteNetLibMessageHandler(msgType, peer, reader);
                handlerDelegate.Invoke(messageHandler);
            }
        }

        public void RegisterServerMessage(short msgType, MessageHandlerDelegate handlerDelegate)
        {
            serverMessageHandlers[msgType] = handlerDelegate;
        }

        public void UnregisterServerMessage(short msgType)
        {
            serverMessageHandlers.Remove(msgType);
        }

        public void RegisterClientMessage(short msgType, MessageHandlerDelegate handlerDelegate)
        {
            clientMessageHandlers[msgType] = handlerDelegate;
        }

        public void UnregisterClientMessage(short msgType)
        {
            clientMessageHandlers.Remove(msgType);
        }
    }
}
