using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Database;
using ChatServer.Handlers;
using ChatServer.Models;

namespace ChatServer
{
    /// <summary>
    /// Punto de entrada del servidor de chat.
    ///
    ///  Puerto 9000 → TCP  (stream persistente + handshake completo)
    ///  Puerto 9001 → UDP  (datagrama sin estado + handshake liviano)
    ///  Puerto 5000 → REST (API para crear/validar salas desde Unity)
    ///
    /// Todos los handlers comparten el mismo diccionario de salas (_rooms)
    /// y el mismo pool de buffers (ByteBufferPool.Shared).
    /// </summary>
    class Program
    {
        // Puertos (pueden cambiarse por args o variables de entorno)
        private const int TCP_PORT  = 9000;
        private const int UDP_PORT  = 9001;
        private const int REST_PORT = 5000;

        // Salas compartidas entre TCP y UDP
        private static readonly ConcurrentDictionary<string, ChatRoom> Rooms = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║     Chat Server  TCP + UDP + REST    ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            // Inicializar BD
            ChatDatabase.Init("chat.db");

            using var cts = new CancellationTokenSource();

            // Cerrar limpiamente con Ctrl+C
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n[Main] Cerrando servidor...");
                cts.Cancel();
            };

            // Lanzar los tres servidores en paralelo
            await Task.WhenAll(
                RunTcpServerAsync(cts.Token),
                RunUdpServerAsync(cts.Token),
                RunRestApiAsync(cts.Token)
            );

            Console.WriteLine("[Main] Servidor detenido.");
        }

        // ── TCP ───────────────────────────────────────────────────

        static async Task RunTcpServerAsync(CancellationToken ct)
        {
            var listener = new TcpListener(IPAddress.Any, TCP_PORT);
            listener.Start();
            Console.WriteLine($"[TCP] Escuchando en :{TCP_PORT}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(ct);
                    Console.WriteLine($"[TCP] Nueva conexión: {client.Client.RemoteEndPoint}");

                    // Cada cliente en su propio Task — no bloquea el accept loop
                    _ = Task.Run(async () =>
                    {
                        var handler = new TcpClientHandler(client, Rooms);
                        await handler.HandleAsync();
                    }, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                listener.Stop();
                Console.WriteLine("[TCP] Servidor detenido");
            }
        }

        // ── UDP ───────────────────────────────────────────────────

        static async Task RunUdpServerAsync(CancellationToken ct)
        {
            using var udpServer = new UdpClient(UDP_PORT);
            Console.WriteLine($"[UDP] Escuchando en :{UDP_PORT}");

            var handler = new UdpServerHandler(udpServer, Rooms, ct);
            await handler.RunAsync();
            Console.WriteLine("[UDP] Servidor detenido");
        }

        // ── REST ──────────────────────────────────────────────────

        static async Task RunRestApiAsync(CancellationToken ct)
        {
            var api = new RestApiHandler(REST_PORT, ct);
            Console.WriteLine($"[API] REST escuchando en :{REST_PORT}");
            await api.RunAsync();
            Console.WriteLine("[API] REST detenido");
        }
    }
}
