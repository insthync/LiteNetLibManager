using LiteNetLib;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public static class WebSocketUtils
    {

        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        /// <param name="wasClean"></param>
        /// <returns></returns>
        public static DisconnectInfo GetDisconnectInfo(int code, string reason, bool wasClean)
        {
            // TODO: Implement this
            WebSocketCloseCode castedCode = (WebSocketCloseCode)code;
            DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed;
            SocketError socketErrorCode = SocketError.ConnectionReset;
            if (castedCode == WebSocketCloseCode.NormalClosure)
                socketErrorCode = SocketError.Success;
            return new DisconnectInfo()
            {
                Reason = disconnectReason,
                SocketErrorCode = socketErrorCode,
            };
        }
    }
}
