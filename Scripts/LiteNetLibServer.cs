using Cysharp.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibServer : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public override string LogTag { get { return (Manager == null ? "(No Manager)" : Manager.LogTag) + "->LiteNetLibServer"; } }
        private bool isNetworkActive;
        public override bool IsNetworkActive { get { return isNetworkActive; } }
        public int ServerPort { get; protected set; }

        public HashSet<long> ConnectionIds { get; private set; } = new HashSet<long>();

        public LiteNetLibServer(LiteNetLibManager manager) : base(manager.Transport)
        {
            Manager = manager;
        }

        public LiteNetLibServer(ITransport transport) : base(transport)
        {

        }

        public void Update()
        {
            if (!IsNetworkActive)
                return;
            while (Transport.ServerReceive(out tempEventData))
            {
                OnServerReceive(tempEventData);
            }
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "Cannot Start Server, network already active");
                return false;
            }
            // Clear and reset request Id
            requestCallbacks.Clear();
            nextRequestId = 1;
            // Store server port, it will be used by local client to connect when start hosting
            ServerPort = port;
            return isNetworkActive = Transport.StartServer(port, maxConnections);
        }

        public void StopServer()
        {
            Transport.StopServer();
            ServerPort = 0;
            isNetworkActive = false;
        }

        public virtual void OnServerReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerConnected peer.ConnectionId: " + eventData.connectionId);
                    ConnectionIds.Add(eventData.connectionId);
                    Manager.OnPeerConnected(eventData.connectionId);
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerDisconnected peer.ConnectionId: " + eventData.connectionId + " disconnectInfo.Reason: " + eventData.disconnectInfo.Reason);
                    ConnectionIds.Remove(eventData.connectionId);
                    Manager.OnPeerDisconnected(eventData.connectionId, eventData.disconnectInfo);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Logging.LogError(LogTag, "OnNetworkError endPoint: " + eventData.endPoint + " socketErrorCode " + eventData.socketError + " errorMessage " + eventData.errorMessage);
                    Manager.OnPeerNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }

        protected override void SendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Transport.ServerSend(connectionId, dataChannel, deliveryMethod, writer);
        }

        public void SendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            WritePacket(writer, msgType, serializer);
            SendMessage(connectionId, dataChannel, deliveryMethod, writer);
        }

        public void SendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            foreach (long connectionId in ConnectionIds)
            {
                SendPacket(connectionId, dataChannel, deliveryMethod, msgType, serializer);
            }
        }

        public bool SendRequest<TRequest>(long connectionId, ushort requestType, TRequest request, ResponseDelegate<INetSerializable> responseDelegate = null, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
        {
            if (!CreateAndWriteRequest(writer, requestType, request, responseDelegate, millisecondsTimeout, extraRequestSerializer))
                return false;
            SendMessage(connectionId, 0, DeliveryMethod.ReliableUnordered, writer);
            return true;
        }

        public async UniTask<AsyncResponseData<TResponse>> SendRequestAsync<TRequest, TResponse>(long connectionId, ushort requestType, TRequest request, int millisecondsTimeout = 30000, SerializerDelegate extraSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            bool done = false;
            AsyncResponseData<TResponse> responseData = default;
            // Create request
            CreateAndWriteRequest(writer, requestType, request, (requestHandler, responseCode, response) =>
            {
                if (!(response is TResponse))
                    response = default(TResponse);
                responseData = new AsyncResponseData<TResponse>(requestHandler, responseCode, (TResponse)response);
                done = true;
            }, millisecondsTimeout, extraSerializer);
            // Send request to target client
            SendMessage(connectionId, 0, DeliveryMethod.ReliableUnordered, writer);
            // Wait for response
            do
            {
                await UniTask.Yield();
            } while (!done);
            // Return response data
            return responseData;
        }
    }
}
