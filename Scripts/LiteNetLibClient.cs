using Cysharp.Threading.Tasks;
using Cysharp.Text;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibClient : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public override string LogTag
        {
            get
            {
                using (var stringBuilder = ZString.CreateStringBuilder(false))
                {
                    if (Manager != null)
                    {
                        stringBuilder.Append(Manager.LogTag);
                    }
                    else
                    {
                        stringBuilder.Append(LiteNetLibManager.TAG_NULL);
                    }
                    stringBuilder.Append('.');
                    stringBuilder.Append('<');
                    stringBuilder.Append('C');
                    stringBuilder.Append('_');
                    stringBuilder.Append(GetType().Name);
                    stringBuilder.Append('>');
                    return stringBuilder.ToString();
                }
            }
        }
        private bool _isNetworkActive;
        public override bool IsNetworkActive { get { return _isNetworkActive; } }
        private byte[] _disconnectData;
        private byte _timeoutCount;
        private bool _reconnecting;
        private string _latestConnectAddress;
        private int _latestConnectPort;

        public LiteNetLibClient(LiteNetLibManager manager) : base()
        {
            Manager = manager;
        }

        public LiteNetLibClient(ITransport transport) : base(transport)
        {

        }

        public void SetDisconnectData(byte[] data)
        {
            _disconnectData = data;
        }

        public void Update()
        {
            if (!IsNetworkActive)
                return;
            while (Transport.ClientReceive(out TransportEventData tempEventData))
            {
                OnClientReceive(tempEventData);
            }
        }

        public bool StartClient(string address, int port)
        {
            if (IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "Cannot Start Client, network already active");
                return false;
            }
            // Clear and reset request Id
            _requestCallbacks.Clear();
            _latestConnectAddress = address;
            _latestConnectPort = port;
            if (_isNetworkActive = Transport.StartClient(address, port))
            {
                OnStartClient();
                return true;
            }
            return false;
        }

        protected virtual void OnStartClient() { }

        public void StopClient()
        {
            Transport.StopClient();
            _isNetworkActive = false;
            OnStopClient();
        }

        protected virtual void OnStopClient() { }

        public virtual void OnClientReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnClientConnected");
                    if (!_reconnecting)
                        Manager.OnClientConnected();
                    _timeoutCount = 0;
                    _reconnecting = false;
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(-1, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, $"OnClientDisconnected peer. disconnectInfo.Reason: {eventData.disconnectInfo.Reason}");
                    if (eventData.disconnectInfo.Reason == DisconnectReason.Timeout && _timeoutCount < 3)
                    {
                        _timeoutCount++;
                        _reconnecting = true;
                        Transport.StartClient(_latestConnectAddress, _latestConnectPort);
                    }
                    else
                    {
                        _timeoutCount = 0;
                        _reconnecting = false;
                        Manager.StopClient();
                        Manager.OnClientDisconnected(eventData.disconnectInfo.Reason, eventData.disconnectInfo.SocketErrorCode, _disconnectData);
                    }
                    _disconnectData = null;
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Logging.LogError(LogTag, $"OnClientNetworkError endPoint: {eventData.endPoint} socketErrorCode {eventData.socketError} errorMessage {eventData.errorMessage}");
                    Manager.OnClientNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }

        public override void SendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Transport.ClientSend(dataChannel, deliveryMethod, writer);
        }

        public void SendMessage(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            SendMessage(-1, dataChannel, deliveryMethod, writer);
        }

        public void SendPacket(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            WritePacket(s_Writer, msgType, serializer);
            // Send packet to server, so connection id will not being used
            SendMessage(dataChannel, deliveryMethod, s_Writer);
        }

        public bool SendRequest<TRequest>(ushort requestType, TRequest request, ResponseDelegate<INetSerializable> responseDelegate = null, int millisecondsTimeout = 30000, SerializerDelegate extraSerializer = null)
            where TRequest : INetSerializable, new()
        {
            if (!CreateAndWriteRequest(s_Writer, requestType, request, responseDelegate, millisecondsTimeout, extraSerializer))
                return false;
            // Send request to server, so connection id will not being used
            SendMessage(0, DeliveryMethod.ReliableUnordered, s_Writer);
            return true;
        }

        public async UniTask<AsyncResponseData<TResponse>> SendRequestAsync<TRequest, TResponse>(ushort requestType, TRequest request, int millisecondsTimeout = 30000, SerializerDelegate extraSerializer = null)
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
            // Send request to server, so connection id will not being used
            SendMessage(0, DeliveryMethod.ReliableUnordered, s_Writer);
            // Wait for response
            do { await UniTask.Delay(100); } while (!done);
            // Return response data
            return responseData;
        }
    }
}
