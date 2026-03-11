using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class RoomManager
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // ── DTOs para JsonUtility ─────────────────────────────────

    [Serializable]
    private class CreateRoomRequest
    {
        public string name;
    }

    [Serializable]
    private class CreateRoomResponse
    {
        public bool   success;
        public string room_id;
        public string name;
    }

    [Serializable]
    private class RoomExistsResponse
    {
        public bool   exists;
        public string room_id;
    }

    // ── Métodos públicos ──────────────────────────────────────

    public static async Task<string> CreateRoomAsync(string roomName = "Sala")
    {
        string url  = $"{GCPConfig.API_URL}/rooms/create";
        string body = JsonUtility.ToJson(new CreateRoomRequest { name = roomName });

        var content  = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        string json   = await response.Content.ReadAsStringAsync();
        var    result = JsonUtility.FromJson<CreateRoomResponse>(json);

        Debug.Log($"[RoomManager] Sala creada: {result.room_id}");
        return result.room_id;
    }

    public static async Task<bool> RoomExistsAsync(string roomId)
    {
        string url = $"{GCPConfig.API_URL}/rooms/{roomId}/exists";

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return false;

        string json   = await response.Content.ReadAsStringAsync();
        var    result = JsonUtility.FromJson<RoomExistsResponse>(json);

        Debug.Log($"[RoomManager] Sala '{roomId}' existe: {result.exists}");
        return result.exists;
    }
}