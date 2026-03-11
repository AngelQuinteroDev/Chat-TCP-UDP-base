using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UI_TCPClient_GCP : MonoBehaviour
{
    [Header("Script del profe (arrastrar TCPClient aqui)")]
    [SerializeField] private TCPClient clientReference;

    [Header("Panel Chat")]
    public ScrollRect     scrollView;
    public Transform      contentParent;
    public TMP_InputField inputMessage;
    public Button         btnSend;
    public Button         btnSendImage;
    public Button         btnSendPdf;
    public Button         btnBack;
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
    private bool    _handshakeDone = false; // true cuando el servidor envio WELCOME

    void Awake()
    {
        _client = clientReference;
    }

    void Start()
    {
        _username = ProtocolSelector.SelectedUsername;
        _roomId   = ProtocolSelector.SelectedRoomCode;

        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_roomId))
        {
            if (lblStatus) lblStatus.text = "Error: faltan datos del menu";
            Debug.LogError("[UI-TCP] Falta username o roomId.");
            return;
        }

        // Verificar que los prefabs esten asignados en el Inspector
        if (!bubbleTextMine)   Debug.LogError("[UI-TCP] bubbleTextMine NO asignado en el Inspector!");
        if (!bubbleTextOther)  Debug.LogError("[UI-TCP] bubbleTextOther NO asignado en el Inspector!");
        if (!bubbleImageMine)  Debug.LogError("[UI-TCP] bubbleImageMine NO asignado en el Inspector!");
        if (!bubbleImageOther) Debug.LogError("[UI-TCP] bubbleImageOther NO asignado en el Inspector!");
        if (!contentParent)    Debug.LogError("[UI-TCP] contentParent NO asignado en el Inspector!");
        if (!scrollView)       Debug.LogError("[UI-TCP] scrollView NO asignado en el Inspector!");

        _client.OnMessageReceived += HandleMessageReceived;
        _client.OnConnected       += HandleConnection;
        _client.OnDisconnected    += HandleDisconnection;

        if (btnSend)      btnSend.onClick.AddListener(SendTextMessage);
        if (btnSendImage) btnSendImage.onClick.AddListener(SendImage);
        if (btnSendPdf)   btnSendPdf.onClick.AddListener(SendPdf);

        if (btnBack) btnBack.onClick.AddListener(GoToProtocolScene);

        if (lblRoomCode) lblRoomCode.text = $"Sala: {_roomId}";
        if (lblProtocol) lblProtocol.text = "TCP";
        if (lblStatus)   lblStatus.text   = "Conectando...";

        ConnectToServer();
    }

    async void ConnectToServer()
    {
        try
        {
            await _client.ConnectToServer(GCPConfig.SERVER_IP, GCPConfig.TCP_PORT);
        }
        catch (Exception ex)
        {
            if (lblStatus) lblStatus.text = $"Error: {ex.Message}";
            Debug.LogError("[UI-TCP] " + ex.Message);
        }
    }

        public void GoToProtocolScene()
    {
        if (_client != null && _client.isConnected)
        {
            _client.Disconnect();
        }

        SceneManager.LoadScene("MainMenu");
    }

    // ── Handlers ──────────────────────────────────────────────

    void HandleConnection()
    {
        // Socket conectado — esperar el HELLO del servidor antes de hacer JOIN
        // NO enviamos JOIN aqui, lo enviamos cuando llegue HELLO
        MainThreadDispatcher.Run(() =>
        {
            if (lblStatus) lblStatus.text = "Esperando servidor...";
            Debug.Log("[UI-TCP] Socket conectado, esperando HELLO del servidor");
        });
    }

    void HandleMessageReceived(string json)
    {
        MainThreadDispatcher.Run(() =>
        {
            try
            {
                // ── HELLO: servidor listo → responder con JOIN ────
                if (json.Contains("\"type\":\"HELLO\""))
                {
                    Debug.Log("[UI-TCP] HELLO recibido, enviando JOIN");
                    string joinMsg = $"{{\"type\":\"JOIN\"," +
                                     $"\"username\":\"{_username}\"," +
                                     $"\"room_id\":\"{_roomId}\"}}";
                    _client.SendMessageAsync(joinMsg);
                    if (lblStatus) lblStatus.text = "Uniendose a la sala...";
                    return;
                }

                // ── WELCOME: handshake completo → habilitar chat ──
                if (json.Contains("\"type\":\"WELCOME\""))
                {
                    _handshakeDone = true;
                    if (lblStatus) lblStatus.text = "Conectado";
                    AddSystemMessage("Conectado a la sala");
                    Debug.Log("[UI-TCP] WELCOME recibido, chat habilitado");
                    return;
                }

                // ── Mensajes de chat (solo si handshake completo) ─
                if (!_handshakeDone) return;

                if (json.Contains("\"type\":\"CHAT\""))
                {
                    // Usar JsonUtility para parsear correctamente (fix Base64 truncado)
                    ChatMsg msg = JsonUtility.FromJson<ChatMsg>(json);

                    string sender  = msg.username ?? "";
                    string content = msg.content ?? "";
                    string ftype   = msg.file_type ?? "text";
                    bool   isMine  = sender == _username;

                    // Filtrar mensajes propios: ya creamos la burbuja al enviar,
                    // el servidor hace broadcast a todos incluyendo al emisor
                    if (isMine)
                    {
                        Debug.Log("[UI-TCP] Mensaje propio recibido del servidor (ignorado, ya se mostro)");
                        return;
                    }

                    Debug.Log($"[UI-TCP] Mostrando burbuja: sender={sender}, content={content}, file_type={ftype}");

                    if (ftype == "image")
                        ShowImageBubble(sender, msg.file_data, isMine);
                    else if (ftype == "pdf" || ftype == "audio")
                        ShowFileBubble(sender, msg.file_name, msg.file_data, isMine);
                    else
                        ShowTextBubble($"{sender}: {content}", isMine);
                }
                else if (json.Contains("\"type\":\"USER_JOINED\""))
                {
                    ChatMsg msg = JsonUtility.FromJson<ChatMsg>(json);
                    AddSystemMessage($"{msg.username} se unio");
                }
                else if (json.Contains("\"type\":\"USER_LEFT\""))
                {
                    ChatMsg msg = JsonUtility.FromJson<ChatMsg>(json);
                    AddSystemMessage($"{msg.username} salio");
                }
                else if (json.Contains("\"type\":\"PONG\""))
                    Debug.Log("[UI-TCP] PONG recibido");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UI-TCP] Error procesando mensaje: {ex.Message}\nJSON: {json.Substring(0, Mathf.Min(200, json.Length))}");
            }
        });
    }

    void HandleDisconnection()
    {
        MainThreadDispatcher.Run(() =>
        {
            _handshakeDone = false;
            AddSystemMessage("Desconectado del servidor");
            if (lblStatus) lblStatus.text = "Desconectado";
        });
    }

    // ── Enviar texto ──────────────────────────────────────────

    public void SendTextMessage()
    {
        if (!_client.isConnected)   { Debug.LogWarning("No conectado"); return; }
        if (!_handshakeDone)        { Debug.LogWarning("Handshake pendiente"); return; }
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

    // ── Enviar imagen ─────────────────────────────────────────

    void SendImage()
    {
        if (!_client.isConnected || !_handshakeDone) return;
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar imagen", "", "png,jpg,jpeg");
#else
        string path = null;
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
        ShowImageBubble("Tu", base64, true);
        ScrollToBottom();
    }

    // ── Enviar PDF ────────────────────────────────────────────

    void SendPdf()
    {
        if (!_client.isConnected || !_handshakeDone) return;
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Seleccionar PDF", "", "pdf");
#else
        string path = null;
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
        ShowFileBubble("Tu", fname, base64, true);
        ScrollToBottom();
    }

    // ── Burbujas ──────────────────────────────────────────────

    void ShowTextBubble(string text, bool isMine)
    {
        GameObject prefab = isMine ? bubbleTextMine : bubbleTextOther;
        if (prefab == null)
        {
            Debug.LogError($"[UI-TCP] Prefab de burbuja de texto es NULL (isMine={isMine}). Asignalo en el Inspector!");
            return;
        }
        if (contentParent == null)
        {
            Debug.LogError("[UI-TCP] contentParent es NULL. Asignalo en el Inspector!");
            return;
        }

        var go  = Instantiate(prefab, contentParent);
        ResetRectTransform(go);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = text;
        Debug.Log($"[UI-TCP] Burbuja de texto creada: '{text}' (isMine={isMine})");
        ScrollToBottom();
    }

    void AddSystemMessage(string text) => ShowTextBubble(text, false);

    void ShowImageBubble(string sender, string base64, bool isMine)
    {
        if (string.IsNullOrEmpty(base64))
        {
            Debug.LogWarning("[UI-TCP] ShowImageBubble: base64 vacio, no se puede mostrar imagen");
            return;
        }

        GameObject prefab = isMine ? bubbleImageMine : bubbleImageOther;
        if (prefab == null)
        {
            Debug.LogError($"[UI-TCP] Prefab de burbuja de imagen es NULL (isMine={isMine}). Asignalo en el Inspector!");
            return;
        }

        try
        {
            byte[]    bytes  = Convert.FromBase64String(base64);
            Texture2D tex    = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);

            var go  = Instantiate(prefab, contentParent);
            ResetRectTransform(go);

            // Buscar el Image para la foto (ignorar el fondo del prefab)
            // Intentar primero por nombre "PhotoImage", si no existe usar GetComponentInChildren
            var photoTransform = go.transform.Find("PhotoImage");
            Image img = photoTransform != null
                ? photoTransform.GetComponent<Image>()
                : go.GetComponentInChildren<Image>();
            if (img) img.sprite = sprite;

            var lbl = go.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = isMine ? "Tu" : sender;

            Debug.Log($"[UI-TCP] Burbuja de imagen creada: sender={sender} (isMine={isMine}), bytes={bytes.Length}");
            ScrollToBottom();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UI-TCP] Error al decodificar imagen: {ex.Message}");
        }
    }

    void ShowFileBubble(string sender, string fileName, string base64, bool isMine)
    {
        GameObject prefab = isMine ? bubbleFileMine : bubbleFileOther;
        if (prefab == null)
        {
            Debug.LogError($"[UI-TCP] Prefab de burbuja de archivo es NULL (isMine={isMine}). Asignalo en el Inspector!");
            return;
        }

        var go  = Instantiate(prefab, contentParent);
        ResetRectTransform(go);
        var lbl = go.GetComponentInChildren<TMP_Text>();
        if (lbl) lbl.text = $"{(isMine ? "Tu" : sender)}: {fileName}";

        var btn = go.GetComponentInChildren<Button>();
        if (btn != null && !string.IsNullOrEmpty(base64))
        {
            string fn  = fileName;
            string b64 = base64;
            btn.onClick.AddListener(() =>
            {
                try
                {
                    byte[] b = Convert.FromBase64String(b64);
                    string savePath = Path.Combine(Application.persistentDataPath, fn);
                    File.WriteAllBytes(savePath, b);
                    Application.OpenURL("file://" + savePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UI-TCP] Error al guardar archivo: {ex.Message}");
                }
            });
        }
        Debug.Log($"[UI-TCP] Burbuja de archivo creada: {fileName} (isMine={isMine})");
        ScrollToBottom();
    }

    /// <summary>
    /// Resetea posición/escala del RectTransform para que la burbuja
    /// aparezca correctamente dentro del Content del ScrollView.
    /// </summary>
    void ResetRectTransform(GameObject go)
    {
        go.SetActive(true);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
            rt.anchoredPosition3D = Vector3.zero;
        }
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (scrollView) scrollView.verticalNormalizedPosition = 0f;
    }

    string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}