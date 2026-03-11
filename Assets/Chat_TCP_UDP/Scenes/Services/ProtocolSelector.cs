using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Flujo correcto del menú principal:
///   [Nueva Sala] → API crea sala → muestra código → usuario lo comparte
///   [Unirse]     → usuario escribe código → valida → habilita Conectar
///   [Conectar]   → elige protocolo → carga escena de chat
/// </summary>
public class ProtocolSelector : MonoBehaviour
{
    [Header("Datos del usuario")]
    public TMP_InputField inputUsername;
    public TMP_Text       lblServerInfo;

    [Header("Gestión de sala")]
    public Button         btnNuevaSala;
    public Button         btnUnirse;
    public TMP_InputField inputRoomCode;
    public TMP_Text       lblRoomCode;
    public TMP_Text       lblStatus;

    [Header("Selector de protocolo")]
    public Toggle         toggleTCP;
    public Toggle         toggleUDP;
    public TMP_Text       lblProtocolDesc;

    [Header("Conectar")]
    public Button         btnConectar;

    [Header("Escenas")]
    public string sceneTCP = "Chat_TCP";
    public string sceneUDP = "Chat_UDP";

    // Datos que pasan a la escena de chat
    public static string SelectedUsername  { get; private set; }
    public static string SelectedRoomCode  { get; private set; }
    public static bool   UseTCP            { get; private set; } = true;

    private bool _roomReady = false;

    void Start()
    {
        if (lblServerInfo)
            lblServerInfo.text = $"Servidor: {GCPConfig.SERVER_IP}";

        toggleTCP.onValueChanged.AddListener(v => { if (v) { UseTCP = true;  UpdateProtocolLabel(); } });
        toggleUDP.onValueChanged.AddListener(v => { if (v) { UseTCP = false; UpdateProtocolLabel(); } });
        toggleTCP.isOn = true;
        UpdateProtocolLabel();

        btnNuevaSala.onClick.AddListener(OnNuevaSalaClicked);
        btnUnirse.onClick.AddListener(OnUnirseClicked);
        btnConectar.onClick.AddListener(OnConectarClicked);

        // Conectar deshabilitado hasta que haya sala lista
        btnConectar.interactable = false;
        lblRoomCode.text = "";
        lblStatus.text   = "";
    }

    void UpdateProtocolLabel()
    {
        if (!lblProtocolDesc) return;
        lblProtocolDesc.text = UseTCP
            ? "TCP — Confiable, con orden garantizado"
            : "UDP — Rapido, sin garantia de entrega";
    }

    // ── Crear sala nueva ──────────────────────────────────────

    async void OnNuevaSalaClicked()
    {
        string username = inputUsername.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            lblStatus.text = "Ingresa tu nombre primero";
            return;
        }

        SetButtonsInteractable(false);
        lblStatus.text = "Creando sala...";

        try
        {
            string roomId = await RoomManager.CreateRoomAsync(username + "s room");

            SelectedRoomCode         = roomId;
            inputRoomCode.text       = roomId;
            lblRoomCode.text         = $"Codigo: {roomId}  (comparte este codigo)";
            lblStatus.text           = "Sala creada correctamente";
            _roomReady               = true;
            btnConectar.interactable = true;
        }
        catch (Exception ex)
        {
            lblStatus.text = $"Error al crear sala: {ex.Message}";
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    // ── Unirse con código ─────────────────────────────────────

    async void OnUnirseClicked()
    {
        string username = inputUsername.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            lblStatus.text = "Ingresa tu nombre primero";
            return;
        }

        string code = inputRoomCode.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            lblStatus.text = "Ingresa el codigo de sala";
            return;
        }

        SetButtonsInteractable(false);
        lblStatus.text = $"Verificando sala {code}...";

        try
        {
            bool exists = await RoomManager.RoomExistsAsync(code);

            if (exists)
            {
                SelectedRoomCode         = code;
                lblRoomCode.text         = $"Sala {code} encontrada";
                lblStatus.text           = "Elige el protocolo y conecta";
                _roomReady               = true;
                btnConectar.interactable = true;
            }
            else
            {
                lblStatus.text           = $"Sala {code} no encontrada";
                lblRoomCode.text         = "";
                _roomReady               = false;
                btnConectar.interactable = false;
            }
        }
        catch (Exception ex)
        {
            lblStatus.text = $"Error: {ex.Message}";
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    // ── Conectar ──────────────────────────────────────────────

    void OnConectarClicked()
    {
        string username = inputUsername.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            lblStatus.text = "Ingresa tu nombre";
            return;
        }

        if (!_roomReady)
        {
            lblStatus.text = "Crea o unete a una sala primero";
            return;
        }

        SelectedUsername  = username;
        _sceneChanging    = true;   // Marcar antes de cambiar escena
        SceneManager.LoadScene(UseTCP ? sceneTCP : sceneUDP);
    }

    // ── Helper ────────────────────────────────────────────────

    // Evita tocar UI después de que la escena cambió
    private bool _sceneChanging = false;

    void SetButtonsInteractable(bool state)
    {
        if (_sceneChanging) return;
        if (btnNuevaSala != null) btnNuevaSala.interactable = state;
        if (btnUnirse    != null) btnUnirse.interactable    = state;
        if (btnConectar  != null) btnConectar.interactable  = state && _roomReady;
    }
}