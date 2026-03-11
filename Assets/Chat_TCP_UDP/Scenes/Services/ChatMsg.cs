using System;

/// <summary>
/// DTO para deserializar los mensajes JSON del servidor con JsonUtility.
/// Reemplaza el ExtractJson manual que truncaba datos Base64.
/// </summary>
[Serializable]
public class ChatMsg
{
    public string type;
    public string username;
    public string room_id;
    public string content;
    public string file_type;
    public string file_data;
    public string file_name;
    public string timestamp;
    public string msg;
    public string server_version;
    public string room_id_created;
}
