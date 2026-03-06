using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Database;

namespace ChatServer.Handlers
{
    /// <summary>
    /// Mini API REST sobre HttpListener (sin dependencias externas).
    ///
    ///  POST /rooms/create          → crea sala, devuelve {room_id, name}
    ///  GET  /rooms/{id}/exists     → {exists: true/false}
    ///  GET  /rooms/{id}/history    → {messages: [...]}
    ///
    /// Unity llama esta API antes de conectarse por TCP/UDP para
    /// crear o validar la sala de chat.
    /// </summary>
    public class RestApiHandler
    {
        private readonly HttpListener _listener;
        private readonly CancellationToken _ct;

        public RestApiHandler(int port, CancellationToken ct)
        {
            _ct = ct;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{port}/");
        }

        public async Task RunAsync()
        {
            _listener.Start();
            Console.WriteLine($"[API] REST API escuchando en puerto configurado");

            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(ctx), _ct);
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[API] Error: {ex.Message}");
                }
            }

            _listener.Stop();
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.ContentType = "application/json; charset=utf-8";

            string path   = req.Url.AbsolutePath.TrimEnd('/');
            string method = req.HttpMethod;

            try
            {
                // POST /rooms/create
                if (method == "POST" && path == "/rooms/create")
                {
                    using var body = req.InputStream;
                    using var sr   = new System.IO.StreamReader(body, Encoding.UTF8);
                    string json    = await sr.ReadToEndAsync();
                    var data       = JsonSerializer.Deserialize<CreateRoomRequest>(json);

                    string roomId  = GenerateCode();
                    string name    = data?.Name ?? "Sala";

                    // Intentar hasta 10 veces si hay colisión de código
                    for (int i = 0; i < 10; i++)
                    {
                        if (ChatDatabase.CreateRoom(roomId, name)) break;
                        roomId = GenerateCode();
                    }

                    await WriteJsonAsync(resp, 200, new { success = true, room_id = roomId, name });
                }

                // GET /rooms/{id}/exists
                else if (method == "GET" && path.StartsWith("/rooms/") && path.EndsWith("/exists"))
                {
                    string roomId = path.Split('/')[2];
                    bool exists   = ChatDatabase.RoomExists(roomId);
                    await WriteJsonAsync(resp, 200, new { exists, room_id = roomId });
                }

                // GET /rooms/{id}/history?limit=50
                else if (method == "GET" && path.StartsWith("/rooms/") && path.EndsWith("/history"))
                {
                    string roomId = path.Split('/')[2];
                    int limit = int.TryParse(req.QueryString["limit"], out var l) ? l : 50;
                    var history = ChatDatabase.GetHistory(roomId, limit);
                    await WriteJsonAsync(resp, 200, new { room_id = roomId, messages = history });
                }

                else
                {
                    await WriteJsonAsync(resp, 404, new { error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(resp, 500, new { error = ex.Message });
            }
        }

        private static async Task WriteJsonAsync(HttpListenerResponse resp, int code, object body)
        {
            resp.StatusCode = code;
            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
            resp.ContentLength64 = data.Length;
            await resp.OutputStream.WriteAsync(data);
            resp.OutputStream.Close();
        }

        private static string GenerateCode(int len = 6)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = new Random();
            var sb  = new System.Text.StringBuilder(len);
            for (int i = 0; i < len; i++)
                sb.Append(chars[rng.Next(chars.Length)]);
            return sb.ToString();
        }

        private class CreateRoomRequest
        {
            public string Name { get; set; }
        }
    }
}
