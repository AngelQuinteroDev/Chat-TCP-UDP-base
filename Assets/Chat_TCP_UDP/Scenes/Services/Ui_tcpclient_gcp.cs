using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reemplaza UI_TCPClient.cs del profe.
///
/// CAMBIOS respecto al original:
///   1. serverAddress y serverPort ahora vienen de GCPConfig (IP del servidor GCP)
///   2. Antes de conectar llama a la REST API para crear o validar la sala
///   3. Los mensajes recibidos se muestran en un panel de chat visual
///   4. Soporta envío de imágenes y archivos (Base64)
///   5. Selector de protocolo TCP/UDP visible en la UI
///
/// LO QUE NO CAMBIA:
///   - TCPClient.cs del profe se usa EXACTAMENTE igual (misma referencia, mismo IClient)
///   - Los métodos ConnectToServer(), SendMessageAsync() y Disconnect() son los del profe
///   - Las interfaces IChatConnection, IClient se usan sin modificar
/// </summary>
public class UI_TCPClient_GCP : MonoBehaviour
{
    // ── Referencias a scripts del PROFE (sin modificar) ──────
    [Header("Scripts del profe (sin modificar)")]
    [SerializeField] private TCPClient clientReference;   // TCPClient.cs original

    // ── Panel Menú ────────────────────────────────────────────
    [Header("Panel Menú")]
    public GameObject panelMenu;
    public TMP_InputField inputUsername;
    public TMP_InputField inputRoomCode;    // Vacío = crear sala nueva
    public Button         btnConnect;
    public TMP_Text       lblStatus;

    // ── Panel Chat ────────────────────────────────────────────
    [Header("Panel Chat")]
    public GameObject     panelChat;
    public ScrollRect     scrollView;
    public Transform      contentParent;   // Padre de las burbujas
    public TMP_InputField inputMessage;
    public Button         btnSend;
    public Button         btnSendImage;
    public Button         btnSendPdf;
    public TMP_Text       lblRoomCode;
    public TMP_Text       lblProtocol;

    // ── Prefabs de burbuja ────────────────────────────────────
    [Header("Prefabs de burbuja")]
    public GameObject bubbleTextMine;
    public GameObject bubbleTextOther;
    public GameObject bubbleImageMine;
    public GameObject bubbleImageOther;
    public GameObject bubbleFileMine;
    public GameObject bubbleFileOther;

    // ── Estado interno ────────────────────────────────────────
    private IClient _client;       // Apunta al TCPClient.cs del profe
    private string  _username;
    private string  _roomId;

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        // Usamos la MISMA interfaz IClient que definió el profe
        _client = clientReference;
    }

    void Start()
    {
        // Suscripción idéntica a la del profe, pero con handlers enriquecidos
        _client.OnMessageReceived += HandleMessageReceived;
        _client.OnConnected       += HandleConnection;
        _client.OnDisconnected    += HandleDisconnection;

        panelMenu.SetActive(true);
        panelChat.SetActive(false);

        btnConnect.onClick.AddListener(OnConnectClicked);
        btnSend.onClick.AddListener(SendTextMessage);
        btnSendImage.onClick.AddListener(SendImage);
        btnSendPdf.onClick.AddListener(SendPdf);
    }

    // ── Conectar ──────────────────────────────────────────────

    async void OnConnectClicked()
    {
        _username = inputUsername.text.Trim();
        if (string.IsNullOrEmpty(_username))
        {
            lblStatus.text = "⚠ Ingresa tu nombre";
            return;
        }

        string code = inputRoomCode.text.Trim().ToUpper();
        btnConnect.interactable = false;
        lblStatus.text = "Conectando…";

        try
        {
            // ── Paso 1: Crear o validar sala via REST API ─────
            if (string.IsNullOrEmpty(code))
            {
                // Sin código → crear sala nueva
                code = await RoomManager.CreateRoomAsync(_username + "'s room");
                lblStatus.text = $"Sala creada: {code}";
            }
            else
            {
                bool exists = await RoomManager.RoomExistsAsync(code);
                if (!exists)
                {
                    lblStatus.text = "⚠ Sala no encontrada";
                    btnConnect.interactable = true;
                    return;
                }
            }

            _roomId = code;

            // ── Paso 2: Conectar por TCP al servidor GCP ──────
            // Igual que el original del profe, solo cambia la IP y puerto
            // que ahora vienen de GCPConfig en vez de valores hardcodeados
            await _client.ConnectToServer(GCPConfig.SERVER_IP, GCPConfig.TCP_PORT);
        }
        catch (Exception ex)
        {
            lblStatus.text = $"Error: {ex.Message}";
            btnConnect.interactable = true;
        }
    }

    // ── Handlers (mismos nombres que el profe, lógica extendida) ─

    void HandleConnection()
    {
        Debug.Log("[UI-TCP] Conectado al servidor GCP");

        MainThreadDispatcher.Run(() =>
        {
            // Enviar JOIN con username y roomId al servidor
            string joinMsg = $"{{\"type\":\"JOIN\",\"username\":\"{_username}\",\"room_id\":\"{_roomId}\"}}";
            _client.SendMessageAsync(joinMsg);

            // Cambiar al panel de chat
            panelMenu.SetActive(false);
            panelChat.SetActive(true);
            lblRoomCode.text  = $"Sala: {_roomId}";
            lblProtocol.text  = "TCP";
        });
    }

    void HandleMessageReceived(string json)
    {
        Debug.Log("[UI-TCP] Recibido: " + json);

        MainThreadDispatcher.Run(() =>
        {
            // Parsear tipo de mensaje
            if (json.Contains("\"type\":\"CHAT\""))
            {
                string sender  = ExtractJson(json, "username");
                string content = ExtractJson(json, "content");
                string ftype   = ExtractJson(json, "file_type");
                bool   isMine  = sender == _username;

                if (ftype == "image")
                {
                    string b64 = ExtractJson(json, "file_data");
                    ShowImageBubble(sender, b64, isMine);
                }
                else if (ftype == "pdf" || ftype == "audio")
                {
                    string fname = ExtractJson(json, "file_name");
                    ShowFileBubble(sender, fname, ExtractJson(json, "file_data"), isMine);
                }
                else
                {
                    ShowTextBubble($"{sender}: {content}", isMine);
                }
            }
            else if (json.Contains("\"type\":\"WELCOME\""))
            {
                AddSystemMessage("✅ Conectado a la sala");
            }
            else if (json.Contains("\"type\":\"USER_JOINED\""))
            {
                string user = ExtractJson(json, "username");
                AddSystemMessage($"🟢 {user} se unió");
            }
            else if (json.Contains("\"type\":\"USER_LEFT\""))
            {
                string user = ExtractJson(json, "username");
                AddSystemMessage($"🔴 {user} salió");
            }
        });
    }

    void HandleDisconnection()
    {
        Debug.Log("[UI-TCP] Desconectado");
        MainThreadDispatcher.Run(() => AddSystemMessage("❌ Desconectado del servidor"));
    }

    // ── Enviar texto ──────────────────────────────────────────

    public void SendTextMessage()
    {
        if (!_client.isConnected) { Debug.Log("No conectado"); return; }
        if (string.IsNullOrEmpty(inputMessage.text)) return;

        string text = inputMessage.text.Trim();
        inputMessage.text = "";

        // Mismo formato que usa el servidor GCP
        string json = $"{{\"type\":\"CHAT\",\"username\":\"{_username}\"," +
                      $"\"room_id\":\"{_roomId}\",\"content\":\"{EscapeJson(text)}\"," +
                      $"\"file_type\":\"text\"}}";

        _client.SendMessageAsync(json);
        ShowTextBubble($"Tú: {text}", true);
        ScrollToBottom();
    }

    // ── Enviar imagen ─────────────────────────────────────────

    void SendImage()
    {
        if (!_client.isConnected) return;

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar imagen", "", "png,jpg,jpeg");
#else
        string path = OpenNativeFilePicker("png,jpg,jpeg");
#endif
        if (string.IsNullOrEmpty(path)) return;

        byte[] bytes  = File.ReadAllBytes(path);
        string base64 = Convert.ToBase64String(bytes);
        string fname  = Path.GetFileName(path);

        string json = $"{{\"type\":\"CHAT\",\"username\":\"{_username}\"," +
                      $"\"room_id\":\"{_roomId}\",\"content\":\"{fname}\"," +
                      $"\"file_type\":\"image\",\"file_name\":\"{fname}\"," +
                      $"\"file_data\":\"{base64}\"}}";

        _client.SendMessageAsync(json);
        ShowImageBubble("Tú", base64, true);
        ScrollToBottom();
    }

    // ── Enviar PDF ────────────────────────────────────────────

    void SendPdf()
    {
        if (!_client.isConnected) return;

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar PDF", "", "pdf");
#else
        string path = OpenNativeFilePicker("pdf");
#endif
        if (string.IsNullOrEmpty(path)) return;

        byte[] bytes  = File.ReadAllBytes(path);
        string base64 = Convert.ToBase64String(bytes);
        string fname  = Path.GetFileName(path);

        string json = $"{{\"type\":\"CHAT\",\"username\":\"{_username}\"," +
                      $"\"room_id\":\"{_roomId}\",\"content\":\"{fname}\"," +
                      $"\"file_type\":\"pdf\",\"file_name\":\"{fname}\"," +
                      $"\"file_data\":\"{base64}\"}}";

        _client.SendMessageAsync(json);
        ShowFileBubble("Tú", fname, base64, true);
        ScrollToBottom();
    }

    // ── Burbujas de chat ──────────────────────────────────────

    void ShowTextBubble(string text, bool isMine)
    {
        var go    = Instantiate(isMine ? bubbleTextMine : bubbleTextOther, contentParent);
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label) label.text = text;
    }

    void AddSystemMessage(string text) => ShowTextBubble(text, false);

    void ShowImageBubble(string sender, string base64, bool isMine)
    {
        if (string.IsNullOrEmpty(base64)) return;

        byte[]    bytes   = Convert.FromBase64String(base64);
        Texture2D tex     = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);

        var go  = Instantiate(isMine ? bubbleImageMine : bubbleImageOther, contentParent);
        var img = go.GetComponentInChildren<Image>();
        if (img) img.sprite = sprite;

        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = isMine ? "Tú" : sender;
    }

    void ShowFileBubble(string sender, string fileName, string base64, bool isMine)
    {
        var go  = Instantiate(isMine ? bubbleFileMine : bubbleFileOther, contentParent);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = $"📎 {(isMine ? "Tú" : sender)}: {fileName}";

        // Botón para guardar el archivo localmente
        var btn = go.GetComponentInChildren<Button>();
        if (btn != null && !string.IsNullOrEmpty(base64))
        {
            btn.onClick.AddListener(() =>
            {
                byte[] bytes = Convert.FromBase64String(base64);
                string savePath = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllBytes(savePath, bytes);
                Debug.Log($"Archivo guardado en: {savePath}");
            });
        }
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (scrollView) scrollView.verticalNormalizedPosition = 0f;
    }

    // ── Utilidades ────────────────────────────────────────────

    /// <summary>Extrae un valor string de un JSON simple sin dependencias.</summary>
    string ExtractJson(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int start = json.IndexOf(search);
        if (start < 0) return "";
        start += search.Length;
        int end = json.IndexOf("\"", start);
        return end < 0 ? "" : json.Substring(start, end - start);
    }

    string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    string OpenNativeFilePicker(string ext)
    {
        // En builds standalone usa NativeFilePicker u otra librería
        Debug.LogWarning("Usa NativeFilePicker en builds. Solo disponible en Editor.");
        return null;
    }
}