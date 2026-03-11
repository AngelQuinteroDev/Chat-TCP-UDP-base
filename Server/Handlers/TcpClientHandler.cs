using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using ChatServer.Database;
using ChatServer.Models;

namespace ChatServer.Handlers
{
    public class TcpClientHandler
    {
        private readonly TcpClient _client;
        private readonly ConcurrentDictionary<string, ChatRoom> _rooms;
        private readonly string _remoteAddr;

        private StreamReader _reader;
        private StreamWriter _writer;
        private ChatRoom     _room;
        private string       _username;

        public TcpClientHandler(TcpClient client, ConcurrentDictionary<string, ChatRoom> rooms)
        {
            _client     = client;
            _rooms      = rooms;
            _remoteAddr = client.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        public async Task HandleAsync()
        {
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, leaveOpen: true);
            _writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = false };

            try
            {
                await SendAsync(ChatMessage.MakeHello());
                Console.WriteLine($"[TCP] HELLO → {_remoteAddr}");

                string joinRaw = await _reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(15));

                if (string.IsNullOrWhiteSpace(joinRaw))
                {
                    await SendAsync(ChatMessage.MakeError("Expected JOIN"));
                    return;
                }

                var join = ChatMessage.FromJson(joinRaw);

                if (join.Type != MessageType.JOIN.ToString())
                {
                    await SendAsync(ChatMessage.MakeError("Expected JOIN"));
                    return;
                }

                _username = join.Username?.Trim();
                string roomId = join.RoomId?.Trim();

                if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(roomId))
                {
                    await SendAsync(ChatMessage.MakeError("username and room_id required"));
                    return;
                }

                if (!ChatDatabase.RoomExists(roomId))
                    ChatDatabase.CreateRoom(roomId, roomId);

                _room = _rooms.GetOrAdd(roomId, id => new ChatRoom(id));
                _room.AddTcpClient(_username, _writer);

                var history = ChatDatabase.GetHistory(roomId, 50);
                await SendAsync(new ChatMessage
                {
                    Type        = MessageType.WELCOME.ToString(),
                    Username    = _username,
                    RoomId      = roomId,
                    History     = history.ToArray(),
                    UsersOnline = _room.GetOnlineUsers().ToArray()
                });

                await _room.BroadcastTcpAsync(new ChatMessage
                {
                    Type      = MessageType.USER_JOINED.ToString(),
                    Username  = _username,
                    Timestamp = DateTime.UtcNow.ToString("o")
                }, exclude: _username);

                Console.WriteLine($"[TCP] {_username} → sala '{roomId}'");

                await ChatLoopAsync();
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                Console.WriteLine($"[TCP] Timeout handshake {_remoteAddr}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] Error {_username ?? _remoteAddr}: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task ChatLoopAsync()
        {
            while (_client.Connected)
            {
                byte[] buffer = ByteBufferPool.Shared.Rent();
                try
                {
                    string line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    var msg = ChatMessage.FromJson(line);
                    await ProcessMessageAsync(msg);
                }
                finally
                {
                    ByteBufferPool.Shared.Return(buffer);
                }
            }
        }

        private async Task ProcessMessageAsync(ChatMessage msg)
        {
            switch (msg.Type)
            {
                case var t when t == MessageType.CHAT.ToString():
                    string ts = ChatDatabase.SaveMessage(
                        _room.RoomId, _username,
                        msg.Content ?? "",
                        msg.FileTypeStr ?? "text",
                        msg.FileName);

                    var broadcast = new ChatMessage
                    {
                        Type        = MessageType.CHAT.ToString(),
                        Username    = _username,
                        Content     = msg.Content,
                        FileTypeStr = msg.FileTypeStr,
                        FileName    = msg.FileName,
                        FileData    = msg.FileData,  
                        Timestamp   = ts
                    };
                    await _room.BroadcastTcpAsync(broadcast);
                    break;

                case var t when t == MessageType.PING.ToString():
                    await SendAsync(ChatMessage.MakePong());
                    break;

                case var t when t == MessageType.CREATE_ROOM.ToString():
                    bool created = ChatDatabase.CreateRoom(msg.RoomId, msg.RoomId);
                    await SendAsync(new ChatMessage
                    {
                        Type          = created
                            ? MessageType.ROOM_CREATED.ToString()
                            : MessageType.ROOM_EXISTS.ToString(),
                        RoomIdCreated = msg.RoomId
                    });
                    break;
            }
        }

        private async Task SendAsync(ChatMessage msg)
        {
            await _writer.WriteAsync(msg.ToJson() + "\n");
            await _writer.FlushAsync();
        }

        private async Task CleanupAsync()
        {
            if (_room != null && _username != null)
            {
                _room.RemoveTcpClient(_username);
                await _room.BroadcastTcpAsync(new ChatMessage
                {
                    Type      = MessageType.USER_LEFT.ToString(),
                    Username  = _username,
                    Timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            _reader?.Dispose();
            _writer?.Dispose();
            _client?.Dispose();
            Console.WriteLine($"[TCP] {_username ?? _remoteAddr} desconectado");
        }
    }
}
