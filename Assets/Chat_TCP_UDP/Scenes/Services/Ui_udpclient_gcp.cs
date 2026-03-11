using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UI_UDPClient_GCP : MonoBehaviour
{
    [Header("Script del profe (sin modificar)")]
    [SerializeField] private UDPClient clientReference;

    [Header("Panel Chat")]
    public ScrollRect     scrollView;
    public Transform      contentParent;
    public TMP_InputField inputMessage;
    public Button         btnSend;
    public Button         btnSendImage;
    public Button         btnSendPdf;
    public Button btnBack;
    public TMP_Text       lblRoomCode;
    public TMP_Text       lblProtocol;
    public TMP_Text       lblStatus;

    [Header("Prefabs de burbuja")]
    public GameObject bubbleTextMine;
    public GameObject bubbleTextOther;
    public GameObject bubbleImageMine;
    public GameObject bubbleImageOther;
    public GameObject bubbleFileMine;
    public GameObject bubbleFileOther;

    private IClient _client;
    private string  _username;
    private string  _roomId;
    private bool    _handshakeDone = false;
    private bool    _sceneChanging  = false; 
    private UdpChunkSender   _chunkSender;
    private UdpChunkReceiver _chunkReceiver;

    public void GoToProtocolScene()
    {
        _sceneChanging = true;
        if (_client != null && _client.isConnected)
            _client.Disconnect();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    

    void Awake()
    {
        _client = clientReference;
    }

    void Start()
    {
        _username = ProtocolSelector.SelectedUsername;
        _roomId   = ProtocolSelector.SelectedRoomCode;

        if (string.IsNullOrEmpty(_username)) _username = "Usuario";
        if (string.IsNullOrEmpty(_roomId))   _roomId   = "SALA01";

        if (lblRoomCode) lblRoomCode.text = "Sala: " + _roomId;
        if (lblProtocol) lblProtocol.text = "UDP";
        if (lblStatus)   lblStatus.text   = "Conectando...";

        _client.OnMessageReceived += HandleMessageReceived;
        _client.OnConnected       += HandleConnection;
        _client.OnDisconnected    += HandleDisconnection;

        if (btnSend)      btnSend.onClick.AddListener(SendTextMessage);
        if (btnSendImage) btnSendImage.onClick.AddListener(SendImage);
        if (btnSendPdf)   btnSendPdf.onClick.AddListener(SendPdf);


        clientReference.connectUsername = _username;
        clientReference.connectRoomId   = _roomId;
        _client.ConnectToServer(GCPConfig.SERVER_IP, GCPConfig.UDP_PORT);

        _chunkSender   = new UdpChunkSender(_client, _username, _roomId);
        _chunkReceiver = new UdpChunkReceiver();
        _chunkReceiver.OnFileComplete += HandleFileComplete;
    }


    void HandleConnection()
    {
        Debug.Log("[UI-UDP] Conectado al servidor GCP");
        MainThreadDispatcher.Run(() => {
            if (lblStatus) lblStatus.text = "Conectado";
            AddSystemMessage("Conectado a la sala " + _roomId);
            _handshakeDone = true;
        });
    }

    void HandleMessageReceived(string json)
    {
        Debug.Log("[UI-UDP] Recibido: " + json.Substring(0, Mathf.Min(120, json.Length)));

        MainThreadDispatcher.Run(() =>
        {
            try
            {
                if (_sceneChanging) return;
                if (!_handshakeDone) return;

                if (json.Contains("\"type\":\"CHUNK\""))
                {
                    _chunkReceiver.HandleChunk(json);
                    return;
                }

                if (json.Contains("\"type\":\"CHAT\""))
                {
                    string sender  = ExtractJson(json, "username");
                    string content = ExtractJson(json, "content");
                    string ftype   = ExtractJson(json, "file_type");
                    bool   isMine  = sender == _username;

                    if (isMine) return;

                    if (ftype == "image")
                        ShowImageBubble(sender, ExtractJson(json, "file_data"), false);
                    else if (ftype == "pdf" || ftype == "audio")
                        ShowFileBubble(sender, ExtractJson(json, "file_name"), ExtractJson(json, "file_data"), false);
                    else
                        ShowTextBubble($"{sender}: {content}", false);
                }
                else if (json.Contains("\"type\":\"USER_JOINED\""))
                    AddSystemMessage($"{ExtractJson(json, "username")} se unio");
                else if (json.Contains("\"type\":\"USER_LEFT\""))
                    AddSystemMessage($"{ExtractJson(json, "username")} salio");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UI-UDP] Error procesando mensaje: {ex.Message}");
            }
        });
    }

    void HandleDisconnection()
    {
        MainThreadDispatcher.Run(() => {
            if (_sceneChanging) return; 
            if (lblStatus) lblStatus.text = "Desconectado";
        });
    }

    public void SendTextMessage()
    {
        if (!_client.isConnected || !_handshakeDone) return;
        if (string.IsNullOrEmpty(inputMessage.text)) return;

        string text = inputMessage.text.Trim();
        inputMessage.text = "";

        string json = $"{{\"type\":\"CHAT\",\"username\":\"{_username}\"," +
                      $"\"room_id\":\"{_roomId}\",\"content\":\"{EscapeJson(text)}\"," +
                      $"\"file_type\":\"text\"}}";

        _client.SendMessageAsync(json);
        ShowTextBubble($"Tu: {text}", true);
        ScrollToBottom();
    }

async void SendImage()
{
    if (!_client.isConnected || !_handshakeDone) return;
#if UNITY_EDITOR
    string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar imagen", "", "png,jpg,jpeg");
#else
    string path = null;
#endif
    if (string.IsNullOrEmpty(path)) return;

    byte[] bytes = File.ReadAllBytes(path);
    string fname = Path.GetFileName(path);

    ShowImageBubble("Tu", Convert.ToBase64String(bytes), true);
    ScrollToBottom();

    if (btnSendImage) btnSendImage.interactable = false;
    await _chunkSender.SendFileAsync(bytes, fname, "image",
        progress => MainThreadDispatcher.Run(() => {
            if (!_sceneChanging && lblStatus)
                lblStatus.text = $"Enviando... {(int)(progress * 100)}%";
        }));

    if (!_sceneChanging)
    {
        if (lblStatus)    lblStatus.text = "Conectado";
        if (btnSendImage) btnSendImage.interactable = true;
    }
}

async void SendPdf()
{
    if (!_client.isConnected || !_handshakeDone) return;
#if UNITY_EDITOR
    string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar PDF", "", "pdf");
#else
    string path = null;
#endif
    if (string.IsNullOrEmpty(path)) return;

    byte[] bytes = File.ReadAllBytes(path);
    string fname = Path.GetFileName(path);

    ShowFileBubble("Tu", fname, Convert.ToBase64String(bytes), true);
    ScrollToBottom();

    if (btnSendPdf) btnSendPdf.interactable = false;
    await _chunkSender.SendFileAsync(bytes, fname, "pdf",
        progress => MainThreadDispatcher.Run(() => {
            if (!_sceneChanging && lblStatus)
                lblStatus.text = $"Enviando... {(int)(progress * 100)}%";
        }));

    if (!_sceneChanging)
    {
        if (lblStatus)  lblStatus.text = "Conectado";
        if (btnSendPdf) btnSendPdf.interactable = true;
    }
}

    void ShowTextBubble(string text, bool isMine)
    {
        if (contentParent == null) { Debug.LogError("[UI-UDP] contentParent es NULL"); return; }
        GameObject prefab = isMine ? bubbleTextMine : bubbleTextOther;
        if (prefab == null) { Debug.LogError($"[UI-UDP] prefab bubble{(isMine?"Mine":"Other")} es NULL"); return; }
        var go  = Instantiate(prefab, contentParent);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = text;
        ScrollToBottom();
    }

    void AddSystemMessage(string text) => ShowTextBubble(text, false);

    void ShowImageBubble(string sender, string base64, bool isMine)
    {
        if (string.IsNullOrEmpty(base64) || contentParent == null) return;
        GameObject prefab = isMine ? bubbleImageMine : bubbleImageOther;
        if (prefab == null) { ShowTextBubble($"[imagen de {sender}]", isMine); return; }

        try
        {
            byte[]    bytes  = Convert.FromBase64String(base64);
            Texture2D tex    = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);

            var go  = Instantiate(prefab, contentParent);
            var img = go.GetComponentInChildren<Image>();
            if (img) img.sprite = sprite;
            ScrollToBottom();
        }
        catch (Exception ex) { Debug.LogError($"[UI-UDP] Error imagen: {ex.Message}"); }
    }

    void ShowFileBubble(string sender, string fname, string base64, bool isMine)
    {
        if (contentParent == null) return;
        GameObject prefab = isMine ? bubbleFileMine : bubbleFileOther;
        if (prefab == null) { ShowTextBubble($"[archivo: {fname}]", isMine); return; }

        var go  = Instantiate(prefab, contentParent);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = fname;

        var btn = go.GetComponentInChildren<Button>();
        if (btn != null && !string.IsNullOrEmpty(base64))
        {
            string fn = fname, b64 = base64;
            btn.onClick.AddListener(() => {
                try {
                    byte[] b = Convert.FromBase64String(b64);
                    string p = Path.Combine(Application.persistentDataPath, fn);
                    File.WriteAllBytes(p, b);
                    Debug.Log($"Guardado: {p}");
                } catch (Exception ex) { Debug.LogError($"[UI-UDP] Error guardando: {ex.Message}"); }
            });
        }
        ScrollToBottom();
    }

    void ScrollToBottom()
    {
        if (scrollView == null) return;
        Canvas.ForceUpdateCanvases();
        scrollView.verticalNormalizedPosition = 0f;
    }

    void HandleFileComplete(UdpChunkReceiver.ChunkMeta meta, byte[] bytes)
{
    MainThreadDispatcher.Run(() =>
    {
        if (_sceneChanging) return;
        if (meta.Username == _username) return; 

        string base64 = Convert.ToBase64String(bytes);

        if (meta.FileType == "image")
            ShowImageBubble(meta.Username, base64, false);
        else
            ShowFileBubble(meta.Username, meta.FileName, base64, false);

        ScrollToBottom();
    });
}

    static string ExtractJson(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int start = json.IndexOf(search);
        if (start < 0) return "";
        start += search.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return "";
        return json.Substring(start, end - start);
    }

    static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}