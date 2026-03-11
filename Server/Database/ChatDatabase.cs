using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using ChatServer.Models;

namespace ChatServer.Database
{
    public static class ChatDatabase
    {
        private static string _dbPath;


        public static void Init(string dbPath = "chat.db")
        {
            _dbPath = dbPath;
            using var conn = GetConn();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS rooms (
                    id         TEXT PRIMARY KEY,
                    name       TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS messages (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    room_id   TEXT    NOT NULL,
                    sender    TEXT    NOT NULL,
                    content   TEXT    NOT NULL,
                    file_type TEXT    NOT NULL DEFAULT 'text',
                    file_name TEXT,
                    timestamp TEXT    NOT NULL,
                    FOREIGN KEY (room_id) REFERENCES rooms(id)
                );
            ";
            cmd.ExecuteNonQuery();
            Console.WriteLine("[DB] Inicializada en: " + Path.GetFullPath(dbPath));
        }

        public static bool CreateRoom(string roomId, string name)
        {
            using var conn = GetConn();
            conn.Open();
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO rooms (id, name, created_at) VALUES ($id, $name, $ts)";
                cmd.Parameters.AddWithValue("$id",   roomId);
                cmd.Parameters.AddWithValue("$name", name);
                cmd.Parameters.AddWithValue("$ts",   DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE violation
            {
                return false;
            }
        }

        public static bool RoomExists(string roomId)
        {
            using var conn = GetConn();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM rooms WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", roomId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }


        public static string SaveMessage(
            string roomId, string sender,
            string content, string fileType = "text",
            string fileName = null)
        {
            string ts = DateTime.UtcNow.ToString("o");
            using var conn = GetConn();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO messages (room_id, sender, content, file_type, file_name, timestamp)
                VALUES ($room, $sender, $content, $ft, $fn, $ts)";
            cmd.Parameters.AddWithValue("$room",    roomId);
            cmd.Parameters.AddWithValue("$sender",  sender);
            cmd.Parameters.AddWithValue("$content", content);
            cmd.Parameters.AddWithValue("$ft",      fileType);
            cmd.Parameters.AddWithValue("$fn",      fileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$ts",      ts);
            cmd.ExecuteNonQuery();
            return ts;
        }


        public static List<HistoryEntry> GetHistory(string roomId, int limit = 50)
        {
            using var conn = GetConn();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT sender, content, file_type, file_name, timestamp
                FROM messages
                WHERE room_id = $room
                ORDER BY id DESC
                LIMIT $limit";
            cmd.Parameters.AddWithValue("$room",  roomId);
            cmd.Parameters.AddWithValue("$limit", limit);

            var entries = new List<HistoryEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(new HistoryEntry
                {
                    Sender   = reader.GetString(0),
                    Content  = reader.GetString(1),
                    FileType = reader.GetString(2),
                    FileName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Timestamp = reader.GetString(4)
                });
            }

            entries.Reverse(); 
            return entries;
        }

        private static SqliteConnection GetConn()
            => new SqliteConnection($"Data Source={_dbPath}");
    }
}
