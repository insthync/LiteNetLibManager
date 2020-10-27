using LiteNetLib;
using LiteNetLib.Utils;
using System;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibClient : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public override string LogTag { get { return $"{(Manager == null ? "(No Manager)" : Manager.LogTag)}->LiteNetLibClient"; } }
        public bool IsClientStarted { get { return Transport.IsClientStarted(); } }

        public LiteNetLibClient(LiteNetLibManager manager) : base(manager.Transport)
        {
            Manager = manager;
        }

        public LiteNetLibClient(ITransport transport) : base(transport)
        {

        }

        public override void Update()
        {
            if (!isNetworkActive)
                return;
            base.Update();
            while (Transport.ClientReceive(out tempEventData))
            {
                OnClientReceive(tempEventData);
            }
        }

        public bool StartClient(string address, int port)
        {
            if (isNetworkActive)
            {
                Logging.LogWarning(LogTag, "Cannot Start Client, network already active");
                return false;
            }
            isNetworkActive = true;
            // Reset acks
            requests.Clear();
            nextAckId = 1;
            return Transport.StartClient(address, port);
        }

        public void StopClient()
        {
            isNetworkActive = false;
            Transport.StopClient();
        }

        public virtual void OnClientReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerConnected");
                    Manager.OnClientConnected();
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerDisconnected peer. disconnectInfo.Reason: " + eventData.disconnectInfo.Reason);
                    Manager.StopClient();
                    Manager.OnClientDisconnected(eventData.disconnectInfo);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Logging.LogError(LogTag, "OnNetworkError endPoint: " + eventData.endPoint + " socketErrorCode " + eventData.socketError);
                    Manager.OnClientNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }

        public uint SendRequest<TRequest, TResponse>(
            ushort msgType,
            TRequest requestMessage,
            AckMessageCallback<TResponse> callback,
            Action<NetDataWriter> extraSerializer = null,
            long duration = 30)
            where TRequest : BaseAckMessage, new()
            where TResponse : BaseAckMessage, new()
        {
            requestMessage.ackId = CreateRequest(callback, duration);
            SendPacket(DeliveryMethod.ReliableOrdered, msgType, (writer) =>
            {
                requestMessage.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
            return requestMessage.ackId;
        }

        public void SendResponse<TResponse>(
            ushort msgType,
            TResponse responseMessage,
            Action<NetDataWriter> extraSerializer = null)
            where TResponse : BaseAckMessage, new()
        {
            SendPacket(DeliveryMethod.ReliableOrdered, msgType, (writer) =>
            {
                responseMessage.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void SendPacket(DeliveryMethod deliveryMethod, ushort msgType, Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            if (serializer != null)
                serializer.Invoke(writer);
            Transport.ClientSend(deliveryMethod, writer);
        }
    }
}
