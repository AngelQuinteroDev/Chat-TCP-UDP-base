using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reemplaza UI_UdpClient.cs del profe.
///
/// CAMBIOS respecto al original:
///   1. serverAddress y serverPort vienen de GCPConfig
///   2. El handshake UDP se enriquece: envía JSON con username y room_id
///   3. Los mensajes se muestran visualmente en el panel de chat
///   4. Soporta imágenes y archivos (Base64)
///
/// LO QUE NO CAMBIA:
///   - UDPClient.cs del profe se usa EXACTAMENTE igual
///   - ConnectToServer() envía "CONNECT" como en el original
///     (el servidor GCP ahora también acepta el JSON enriquecido)
///   - Las interfaces IClient, IChatConnection sin modificar
/// </summary>
public class UI_UDPClient_GCP : MonoBehaviour
{
    // ── Script del PROFE (sin modificar) ──────────────────────
    [Header("Script del profe (sin modificar)")]
    [SerializeField] private UDPClient clientReference;  // UDPClient.cs original

    // ── Panel Menú ────────────────────────────────────────────
    [Header("Panel Menú")]
    public GameObject     panelMenu;
    public TMP_InputField inputUsername;
    public TMP_InputField inputRoomCode;
    public Button         btnConnect;
    public TMP_Text       lblStatus;

    // ── Panel Chat ────────────────────────────────────────────
    [Header("Panel Chat")]
    public GameObject     panelChat;
    public ScrollRect     scrollView;
    public Transform      contentParent;
    public TMP_InputField inputMessage;
    public Button         btnSend;
    public Button         btnSendImage;
    public Button         btnSendPdf;
    public TMP_Text       lblRoomCode;
    public TMP_Text       lblProtocol;

    // ── Prefabs ───────────────────────────────────────────────
    [Header("Prefabs de burbuja")]
    public GameObject bubbleTextMine;
    public GameObject bubbleTextOther;
    public GameObject bubbleImageMine;
    public GameObject bubbleImageOther;
    public GameObject bubbleFileMine;
    public GameObject bubbleFileOther;

    // ── Estado ────────────────────────────────────────────────
    private IClient _client;
    private string  _username;
    private string  _roomId;

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        _client = clientReference;  // Misma interfaz IClient del profe
    }

    void Start()
    {
        // Suscripción idéntica a la del profe
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
            // ── Paso 1: REST API para sala ────────────────────
            if (string.IsNullOrEmpty(code))
            {
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

            // ── Paso 2: Conectar por UDP ──────────────────────
            // UDPClient.cs del profe envía "CONNECT" en ConnectToServer.
            // El servidor GCP ahora también acepta el JSON enriquecido
            // que enviamos justo después en HandleConnection.
            await _client.ConnectToServer(GCPConfig.SERVER_IP, GCPConfig.UDP_PORT);
        }
        catch (Exception ex)
        {
            lblStatus.text = $"Error: {ex.Message}";
            btnConnect.interactable = true;
        }
    }

    // ── Handlers ──────────────────────────────────────────────

    void HandleConnection()
    {
        Debug.Log("[UI-UDP] Conectado al servidor GCP");

        MainThreadDispatcher.Run(() =>
        {
            // Enviar JOIN con usuario y sala
            string joinMsg = $"{{\"type\":\"CONNECT\",\"username\":\"{_username}\"," +
                             $"\"room_id\":\"{_roomId}\"}}";
            _client.SendMessageAsync(joinMsg);

            panelMenu.SetActive(false);
            panelChat.SetActive(true);
            lblRoomCode.text = $"Sala: {_roomId}";
            lblProtocol.text = "UDP";
        });
    }

    void HandleMessageReceived(string json)
    {
        Debug.Log("[UI-UDP] Recibido: " + json);

        MainThreadDispatcher.Run(() =>
        {
            if (json.Contains("\"type\":\"CHAT\""))
            {
                string sender  = ExtractJson(json, "username");
                string content = ExtractJson(json, "content");
                string ftype   = ExtractJson(json, "file_type");
                bool   isMine  = sender == _username;

                if (ftype == "image")
                    ShowImageBubble(sender, ExtractJson(json, "file_data"), isMine);
                else if (ftype == "pdf" || ftype == "audio")
                    ShowFileBubble(sender, ExtractJson(json, "file_name"),
                                   ExtractJson(json, "file_data"), isMine);
                else
                    ShowTextBubble($"{sender}: {content}", isMine);
            }
            else if (json.Contains("\"type\":\"CONNECTED\""))
                AddSystemMessage("✅ Conectado a la sala (UDP)");
            else if (json.Contains("\"type\":\"USER_JOINED\""))
                AddSystemMessage($"🟢 {ExtractJson(json, "username")} se unió");
            else if (json.Contains("\"type\":\"USER_LEFT\""))
                AddSystemMessage($"🔴 {ExtractJson(json, "username")} salió");
        });
    }

    void HandleDisconnection()
    {
        Debug.Log("[UI-UDP] Desconectado");
        MainThreadDispatcher.Run(() => AddSystemMessage("❌ Desconectado del servidor"));
    }

    // ── Enviar mensajes ───────────────────────────────────────

    public void SendTextMessage()
    {
        if (!_client.isConnected) return;
        if (string.IsNullOrEmpty(inputMessage.text)) return;

        string text = inputMessage.text.Trim();
        inputMessage.text = "";

        string json = $"{{\"type\":\"CHAT\",\"username\":\"{_username}\"," +
                      $"\"room_id\":\"{_roomId}\",\"content\":\"{EscapeJson(text)}\"," +
                      $"\"file_type\":\"text\"}}";

        _client.SendMessageAsync(json);
        ShowTextBubble($"Tú: {text}", true);
        ScrollToBottom();
    }

    void SendImage()
    {
        if (!_client.isConnected) return;

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar imagen", "", "png,jpg,jpeg");
#else
        string path = null;
        Debug.LogWarning("Usa NativeFilePicker en builds standalone.");
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

    void SendPdf()
    {
        if (!_client.isConnected) return;

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar PDF", "", "pdf");
#else
        string path = null;
        Debug.LogWarning("Usa NativeFilePicker en builds standalone.");
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

    // ── Burbujas ──────────────────────────────────────────────

    void ShowTextBubble(string text, bool isMine)
    {
        var go = Instantiate(isMine ? bubbleTextMine : bubbleTextOther, contentParent);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = text;
    }

    void AddSystemMessage(string text) => ShowTextBubble(text, false);

    void ShowImageBubble(string sender, string base64, bool isMine)
    {
        if (string.IsNullOrEmpty(base64)) return;
        byte[]    bytes  = Convert.FromBase64String(base64);
        Texture2D tex    = new Texture2D(2, 2);
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

        var btn = go.GetComponentInChildren<Button>();
        if (btn != null && !string.IsNullOrEmpty(base64))
        {
            string fn = fileName, b64 = base64;
            btn.onClick.AddListener(() =>
            {
                byte[] b = Convert.FromBase64String(b64);
                string p = Path.Combine(Application.persistentDataPath, fn);
                File.WriteAllBytes(p, b);
                Debug.Log($"Guardado: {p}");
            });
        }
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (scrollView) scrollView.verticalNormalizedPosition = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────

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
}