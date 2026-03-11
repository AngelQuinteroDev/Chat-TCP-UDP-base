using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Database;
using ChatServer.Models;

namespace ChatServer.Handlers
{
    /// <summary>
    /// Servidor UDP centralizado para el chat.
    ///
    ///  HANDSHAKE UDP (sin estado nativo)
    ///  ──────────────────────────────────
    ///  UDP no tiene conexión persistente como TCP, así que el
    ///  "handshake" es un intercambio acordado de mensajes:
    ///
    ///  1. Cliente envía  CONNECT {username, room_id}
    ///  2. Servidor guarda el endpoint del cliente, responde CONNECTED + historial
    ///  3. El servidor ahora sabe a quién reenviar los broadcasts
    ///
    ///  OBJECT POOL
    ///  ───────────
    ///  Cada datagrama recibido pide un buffer al pool, lo procesa,
    ///  y lo devuelve. Esto evita alocaciones en el hot-path de red.
    ///
    ///  KEEPALIVE UDP
    ///  ─────────────
    ///  UDP no detecta desconexiones. Usamos PING/PONG: si un cliente
    ///  no responde PONG en N segundos, se elimina de la sala.
    /// </summary>
    public class UdpServerHandler
    {
        private readonly UdpClient _server;
        private readonly ConcurrentDictionary<string, ChatRoom> _rooms;
        private readonly CancellationToken _ct;

        // Keepalive: endpoint → último timestamp de actividad
        private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();

        public UdpServerHandler(UdpClient server,
                                ConcurrentDictionary<string, ChatRoom> rooms,
                                CancellationToken ct)
        {
            _server = server;
            _rooms  = rooms;
            _ct     = ct;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("[UDP] Servidor escuchando...");

            // Keepalive checker en background
            _ = Task.Run(KeepAliveLoopAsync, _ct);

            while (!_ct.IsCancellationRequested)
            {
                // Pedir buffer al pool (Object Pool)
                byte[] poolBuffer = ByteBufferPool.Shared.Rent();
                try
                {
                    UdpReceiveResult result = await _server.ReceiveAsync();
                    // Copiar al buffer del pool para procesamiento
                    int len = Math.Min(result.Buffer.Length, poolBuffer.Length);
                    Buffer.BlockCopy(result.Buffer, 0, poolBuffer, 0, len);

                    // Actualizar keepalive
                    string endpointKey = result.RemoteEndPoint.ToString();
                    _lastSeen[endpointKey] = DateTime.UtcNow;

                    // Procesar en background para no bloquear el receive
                    byte[] copy = new byte[len];
                    Buffer.BlockCopy(poolBuffer, 0, copy, 0, len);
                    _ = Task.Run(() => HandleDatagramAsync(copy, result.RemoteEndPoint), _ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP] Error receive: {ex.Message}");
                }
                finally
                {
                    ByteBufferPool.Shared.Return(poolBuffer);
                }
            }
        }

        private async Task HandleDatagramAsync(byte[] data, IPEndPoint from)
        {
            ChatMessage msg;
            try { msg = ChatMessage.FromJson(Encoding.UTF8.GetString(data)); }
            catch { return; }

            string roomId   = msg.RoomId   ?? "default";
            string username = msg.Username ?? "unknown";

            switch (msg.Type)
            {
                // ── HANDSHAKE ──────────────────────────────────────
                case var t when t == MessageType.CONNECT.ToString():
                    if (!ChatDatabase.RoomExists(roomId))
                        ChatDatabase.CreateRoom(roomId, roomId);

                    var room = _rooms.GetOrAdd(roomId, id => new ChatRoom(id));
                    room.AddUdpClient(username, from);

                    var history = ChatDatabase.GetHistory(roomId, 50);

                    await SendAsync(new ChatMessage
                    {
                        Type        = MessageType.CONNECTED.ToString(),
                        Username    = username,
                        RoomId      = roomId,
                        History     = history.ToArray(),
                        UsersOnline = room.GetOnlineUsers().ToArray()
                    }, from);

                    await room.BroadcastUdpAsync(new ChatMessage
                    {
                        Type      = MessageType.USER_JOINED.ToString(),
                        Username  = username,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    }, _server, exclude: from);

                    Console.WriteLine($"[UDP] {username} → sala '{roomId}' desde {from}");
                    break;

                // ── CHAT ───────────────────────────────────────────
                case var t when t == MessageType.CHAT.ToString():
                    if (!_rooms.TryGetValue(roomId, out var chatRoom)) return;

                    string ts = ChatDatabase.SaveMessage(
                        roomId, username,
                        msg.Content ?? "",
                        msg.FileTypeStr ?? "text",
                        msg.FileName);

                    await chatRoom.BroadcastUdpAsync(new ChatMessage
                    {
                        Type        = MessageType.CHAT.ToString(),
                        Username    = username,
                        Content     = msg.Content,
                        FileTypeStr = msg.FileTypeStr,
                        FileName    = msg.FileName,
                        FileData    = msg.FileData,
                        Timestamp   = ts
                    }, _server);
                    break;

                // ── KEEPALIVE ──────────────────────────────────────
                case var t when t == MessageType.PING.ToString():
                    await SendAsync(ChatMessage.MakePong(), from);
                    break;

                // ── DESCONEXIÓN VOLUNTARIA ─────────────────────────
                case var t when t == MessageType.USER_LEFT.ToString():
                    if (_rooms.TryGetValue(roomId, out var leaveRoom))
                    {
                        leaveRoom.RemoveUdpClient(username);
                        await leaveRoom.BroadcastUdpAsync(new ChatMessage
                        {
                            Type      = MessageType.USER_LEFT.ToString(),
                            Username  = username,
                            Timestamp = DateTime.UtcNow.ToString("o")
                        }, _server);
                    }
                    break;
            }
        }

        /// <summary>
        /// Keepalive: cada 30 s verifica si algún cliente dejó
        /// de enviar PING. Si lleva más de 90 s inactivo, se elimina.
        /// </summary>
        private async Task KeepAliveLoopAsync()
        {
            while (!_ct.IsCancellationRequested)
            {
                await Task.Delay(30_000, _ct);
                var cutoff = DateTime.UtcNow.AddSeconds(-90);
                foreach (var (key, lastTime) in _lastSeen)
                {
                    if (lastTime < cutoff)
                    {
                        _lastSeen.TryRemove(key, out _);
                        Console.WriteLine($"[UDP] Keepalive timeout: {key}");
                    }
                }
            }
        }

        private async Task SendAsync(ChatMessage msg, IPEndPoint to)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg.ToJson());
            await _server.SendAsync(data, data.Length, to);
        }
    }
}
