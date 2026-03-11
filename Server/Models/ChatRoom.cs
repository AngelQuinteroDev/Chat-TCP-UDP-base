using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ChatServer.Models;

namespace ChatServer.Models
{
    public class ChatRoom
    {
        public string RoomId { get; }

        private readonly ConcurrentDictionary<string, StreamWriter> _tcpClients = new();


        private readonly ConcurrentDictionary<string, IPEndPoint> _udpClients = new();

        public ChatRoom(string roomId)
        {
            RoomId = roomId;
        }


        public void AddTcpClient(string username, StreamWriter writer)
            => _tcpClients[username] = writer;

        public void RemoveTcpClient(string username)
            => _tcpClients.TryRemove(username, out _);

        public List<string> GetOnlineUsers()
        {
            var users = new List<string>(_tcpClients.Keys);
            foreach (var u in _udpClients.Keys)
                if (!users.Contains(u)) users.Add(u);
            return users;
        }

        public async Task BroadcastTcpAsync(ChatMessage msg, string exclude = null)
        {
            string json = msg.ToJson() + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);

            foreach (var (username, writer) in _tcpClients)
            {
                if (username == exclude) continue;
                try
                {
                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                }
                catch { /* cliente desconectado, se limpia en su handler */ }
            }
        }

        public void AddUdpClient(string username, IPEndPoint endpoint)
            => _udpClients[username] = endpoint;

        public void RemoveUdpClient(string username)
            => _udpClients.TryRemove(username, out _);

        public async Task BroadcastUdpAsync(ChatMessage msg, System.Net.Sockets.UdpClient server,
                                             IPEndPoint exclude = null)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg.ToJson());
            foreach (var (_, endpoint) in _udpClients)
            {
                if (exclude != null && endpoint.Equals(exclude)) continue;
                try { await server.SendAsync(data, data.Length, endpoint); }
                catch { }
            }
        }
    }
}
