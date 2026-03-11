using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatServer.Models
{
    /// <summary>
    /// Tipos de mensaje que circulan por el protocolo.
    /// Tanto el servidor como los clientes Unity usan este mismo enum.
    /// </summary>
    public enum MessageType
    {
        // ── Handshake ──────────────────────────────
        HELLO,       // Servidor → Cliente al conectarse (TCP)
        JOIN,        // Cliente → Servidor: quiero entrar a una sala
        WELCOME,     // Servidor → Cliente: bienvenido + historial
        CONNECT,     // Cliente → Servidor UDP: primer datagrama
        CONNECTED,   // Servidor → Cliente UDP: handshake OK

        // ── Sala ───────────────────────────────────
        CREATE_ROOM, // Cliente pide crear sala
        ROOM_CREATED,
        ROOM_EXISTS,
        ROOM_NOT_FOUND,

        // ── Chat ───────────────────────────────────
        CHAT,        // Mensaje de texto o archivo
        USER_JOINED,
        USER_LEFT,

        // ── Keepalive ──────────────────────────────
        PING,
        PONG,

        // ── Errores ────────────────────────────────
        ERROR
    }

    /// <summary>
    /// Tipos de contenido de un CHAT (texto, imagen, pdf, audio…).
    /// </summary>
    public enum FileType
    {
        text,
        image,
        pdf,
        audio
    }

    /// <summary>
    /// Sobre que viaja por el socket (JSON con \n como delimitador).
    /// Para archivos, FileData contiene el Base64 del binario.
    /// </summary>
    public class ChatMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }            // Nombre del enum MessageType

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }         // Texto del mensaje o nombre de archivo

        [JsonPropertyName("file_type")]
        public string FileTypeStr { get; set; } = "text";

        [JsonPropertyName("file_data")]
        public string FileData { get; set; }        // Base64 del binario (imagen, pdf, audio)

        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("history")]
        public HistoryEntry[] History { get; set; }  // Solo en WELCOME

        [JsonPropertyName("users_online")]
        public string[] UsersOnline { get; set; }    // Solo en WELCOME

        [JsonPropertyName("room_id_created")]
        public string RoomIdCreated { get; set; }    // Solo en ROOM_CREATED

        [JsonPropertyName("server_version")]
        public string ServerVersion { get; set; }    // Solo en HELLO

        [JsonPropertyName("msg")]
        public string Msg { get; set; }              // Solo en ERROR

        // ── Helpers ────────────────────────────────────────────────

        public static ChatMessage FromJson(string json)
            => JsonSerializer.Deserialize<ChatMessage>(json);

        public string ToJson()
            => JsonSerializer.Serialize(this);

        public static ChatMessage MakeHello() => new ChatMessage
        {
            Type = MessageType.HELLO.ToString(),
            ServerVersion = "1.0",
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        public static ChatMessage MakeError(string msg) => new ChatMessage
        {
            Type = MessageType.ERROR.ToString(),
            Msg = msg
        };

        public static ChatMessage MakePong() => new ChatMessage
        {
            Type = MessageType.PONG.ToString(),
            Timestamp = DateTime.UtcNow.ToString("o")
        };
    }

    /// <summary>
    /// Entrada de historial devuelta en WELCOME.
    /// </summary>
    public class HistoryEntry
    {
        [JsonPropertyName("sender")]
        public string Sender { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("file_type")]
        public string FileType { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }
}
