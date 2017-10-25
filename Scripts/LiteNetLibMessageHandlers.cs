using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibHighLevel.Messages;

namespace LiteNetLibHighLevel
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
        
        public void ServerReadPacket(NetPeer peer, NetDataReader reader)
        {
            ReadPacket(peer, reader, serverMessageHandlers);
        }

        public void ClientReadPacket(NetPeer peer, NetDataReader reader)
        {
            ReadPacket(peer, reader, clientMessageHandlers);
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
