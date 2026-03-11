using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatServer.Models
{
    public enum MessageType
    {
        HELLO,       
        JOIN,        
        WELCOME,     
        CONNECT,     
        CONNECTED,   

        CREATE_ROOM, 
        ROOM_CREATED,
        ROOM_EXISTS,
        ROOM_NOT_FOUND,

        CHAT,        
        USER_JOINED,
        USER_LEFT,

        PING,
        PONG,

        ERROR
    }

    public enum FileType
    {
        text,
        image,
        pdf,
        audio
    }

    public class ChatMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }           

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }        

        [JsonPropertyName("file_type")]
        public string FileTypeStr { get; set; } = "text";

        [JsonPropertyName("file_data")]
        public string FileData { get; set; }        
        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("history")]
        public HistoryEntry[] History { get; set; } 

        [JsonPropertyName("users_online")]
        public string[] UsersOnline { get; set; }  

        [JsonPropertyName("room_id_created")]
        public string RoomIdCreated { get; set; }    

        [JsonPropertyName("server_version")]
        public string ServerVersion { get; set; }    

        [JsonPropertyName("msg")]
        public string Msg { get; set; }         

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
