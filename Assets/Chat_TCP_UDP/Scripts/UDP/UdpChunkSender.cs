using System;
using System.Threading.Tasks;
using UnityEngine;


public class UdpChunkSender
{

    private const int CHUNK_BYTES = 8 * 1024;

    private const int DELAY_MS = 20;

    private readonly IClient _client;
    private readonly string  _username;
    private readonly string  _roomId;

    public UdpChunkSender(IClient client, string username, string roomId)
    {
        _client   = client;
        _username = username;
        _roomId   = roomId;
    }

    /// <summary>
    public async Task SendFileAsync(byte[] fileBytes, string fileName, string fileType,
                                    Action<float> onProgress = null)
    {
        if (!_client.isConnected)
        {
            Debug.LogWarning("[UdpChunkSender] No conectado, cancelando envío");
            return;
        }

        string fullBase64   = Convert.ToBase64String(fileBytes);
        int    totalLen     = fullBase64.Length;
        int    totalChunks  = Mathf.CeilToInt((float)totalLen / CHUNK_BYTES);
        string transferId   = Guid.NewGuid().ToString("N").Substring(0, 8); 

        Debug.Log($"[UdpChunkSender] Enviando '{fileName}' — {fileBytes.Length} bytes → {totalChunks} chunks (id={transferId})");

        for (int i = 0; i < totalChunks; i++)
        {
            int start      = i * CHUNK_BYTES;
            int length     = Mathf.Min(CHUNK_BYTES, totalLen - start);
            string segment = fullBase64.Substring(start, length);

            string json = "{"
                + $"\"type\":\"CHUNK\","
                + $"\"transfer_id\":\"{transferId}\","
                + $"\"chunk_index\":{i},"
                + $"\"total_chunks\":{totalChunks},"
                + $"\"username\":\"{EscapeJson(_username)}\","
                + $"\"room_id\":\"{EscapeJson(_roomId)}\","
                + $"\"file_name\":\"{EscapeJson(fileName)}\","
                + $"\"file_type\":\"{fileType}\","
                + $"\"data\":\"{segment}\""
                + "}";

            _client.SendMessageAsync(json);

            onProgress?.Invoke((float)(i + 1) / totalChunks);
            
            await Task.Delay(DELAY_MS);
        }

        Debug.Log($"[UdpChunkSender] '{fileName}' enviado completamente ({totalChunks} chunks)");
    }

    private static string EscapeJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}