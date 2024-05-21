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
        private bool _isNetworkActive;
        public override bool IsNetworkActive { get { return _isNetworkActive; } }
        public int ServerPort { get; protected set; }
        public HashSet<long> ConnectionIds { get; private set; } = new HashSet<long>();

        public LiteNetLibServer(LiteNetLibManager manager) : base()
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
            while (Transport.ServerReceive(out TransportEventData tempEventData))
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
            _requestCallbacks.Clear();
            // Store server port, it will be used by local client to connect when start hosting
            ServerPort = port;
            if (_isNetworkActive = Transport.StartServer(port, maxConnections))
            {
                OnStartServer();
                return true;
            }
            return false;
        }

        protected virtual void OnStartServer() { }

        public void StopServer()
        {
            Transport.StopServer();
            ServerPort = 0;
            _isNetworkActive = false;
            OnStopServer();
        }

        protected virtual void OnStopServer() { }

        public virtual void OnServerReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, $"OnPeerConnected peer.ConnectionId: {eventData.connectionId}");
                    ConnectionIds.Add(eventData.connectionId);
                    Manager.OnPeerConnected(eventData.connectionId);
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, $"OnPeerDisconnected peer.ConnectionId: {eventData.connectionId} disconnectInfo.Reason: {eventData.disconnectInfo.Reason}");
                    ConnectionIds.Remove(eventData.connectionId);
                    Manager.OnPeerDisconnected(eventData.connectionId, eventData.disconnectInfo.Reason, eventData.disconnectInfo.SocketErrorCode);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Logging.LogError(LogTag, $"OnPeerNetworkError endPoint: {eventData.endPoint} socketErrorCode {eventData.socketError} errorMessage {eventData.errorMessage}");
                    Manager.OnPeerNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }

        public override void SendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Transport.ServerSend(connectionId, dataChannel, deliveryMethod, writer);
        }

        public void SendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            WritePacket(s_Writer, msgType, serializer);
            SendMessage(connectionId, dataChannel, deliveryMethod, s_Writer);
        }

        public void SendMessageToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            foreach (long connectionId in ConnectionIds)
            {
                SendMessage(connectionId, dataChannel, deliveryMethod, writer);
            }
        }

        public void SendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            WritePacket(s_Writer, msgType, serializer);
            SendMessageToAllConnections(dataChannel, deliveryMethod, s_Writer);
        }

        public bool SendRequest<TRequest>(long connectionId, ushort requestType, TRequest request, ResponseDelegate<INetSerializable> responseDelegate = null, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
        {
            if (!CreateAndWriteRequest(s_Writer, requestType, request, responseDelegate, millisecondsTimeout, extraRequestSerializer))
                return false;
            SendMessage(connectionId, 0, DeliveryMethod.ReliableUnordered, s_Writer);
            return true;
        }

        public async UniTask<AsyncResponseData<TResponse>> SendRequestAsync<TRequest, TResponse>(long connectionId, ushort requestType, TRequest request, int millisecondsTimeout = 30000, SerializerDelegate extraSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            bool done = false;
            AsyncResponseData<TResponse> responseData = default;
            // Create request
            CreateAndWriteRequest(s_Writer, requestType, request, (requestHandler, responseCode, response) =>
            {
                if (!(response is TResponse))
                    response = default(TResponse);
                responseData = new AsyncResponseData<TResponse>(requestHandler, responseCode, (TResponse)response);
                done = true;
            }, millisecondsTimeout, extraSerializer);
            // Send request to target client
            SendMessage(connectionId, 0, DeliveryMethod.ReliableUnordered, s_Writer);
            // Wait for response
            do { await UniTask.Delay(100); } while (!done);
            // Return response data
            return responseData;
        }
    }
}
