using System.Net;
using System.Net.Sockets;

namespace QuoteService
{
    public class Stream : NetworkStream
    {
        public Stream(Socket socket) : base(socket)
        {
        }

        public bool IsConnected => Socket.Connected;
        public EndPoint RemoteEndPoint => Socket.RemoteEndPoint;
    }
}