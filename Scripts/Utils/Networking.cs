using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager.Utils
{
    public class Networking
    {
        public static int GetFreePort()
        {
            Socket socketV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socketV4.Bind(new IPEndPoint(IPAddress.Any, 0));
            int port = ((IPEndPoint)socketV4.LocalEndPoint).Port;
            socketV4.Close();
            return port;
        }
    }
}
