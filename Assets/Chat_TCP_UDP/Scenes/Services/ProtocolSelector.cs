using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Script para la escena del Menú Principal.
///
/// El usuario elige aquí:
///   1. Su nombre de usuario
///   2. El protocolo: TCP o UDP  (requisito explícito del enunciado)
///   3. Código de sala (vacío = crear nueva)
///
/// Al pulsar Conectar carga la escena del protocolo elegido.
/// </summary>
public class ProtocolSelector : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputUsername;
    public TMP_InputField inputRoomCode;
    public Toggle         toggleTCP;
    public Toggle         toggleUDP;
    public Button         btnConnect;
    public TMP_Text       lblStatus;
    public TMP_Text       lblServerInfo;

    [Header("Nombres de escenas")]
    public string sceneTCP = "Chat_TCP";
    public string sceneUDP = "Chat_UDP";

    // Guardar selección para que la escena de chat la lea
    public static string  SelectedUsername  { get; private set; }
    public static string  SelectedRoomCode  { get; private set; }
    public static bool    UseTCP            { get; private set; } = true;

    void Start()
    {
        // Mostrar IP del servidor configurada
        lblServerInfo.text = $"Servidor: {GCPConfig.SERVER_IP}";

        toggleTCP.onValueChanged.AddListener(v => { if (v) UseTCP = true;  UpdateLabel(); });
        toggleUDP.onValueChanged.AddListener(v => { if (v) UseTCP = false; UpdateLabel(); });
        toggleTCP.isOn = true;
        UpdateLabel();

        btnConnect.onClick.AddListener(OnConnectClicked);
    }

    void UpdateLabel()
    {
        lblStatus.text = UseTCP
            ? "Protocolo seleccionado: TCP  (confiable, con orden garantizado)"
            : "Protocolo seleccionado: UDP  (rápido, sin garantía de entrega)";
    }

    async void OnConnectClicked()
    {
        string username = inputUsername.text.Trim();
        string code     = inputRoomCode.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(username))
        {
            lblStatus.text = "⚠ Ingresa tu nombre de usuario";
            return;
        }

        btnConnect.interactable = false;
        lblStatus.text = "Verificando sala…";

        try
        {
            if (string.IsNullOrEmpty(code))
            {
                // Crear sala nueva
                code = await RoomManager.CreateRoomAsync(username + "'s room");
                lblStatus.text = $"Sala creada: {code} — cargando…";
            }
            else
            {
                // Verificar que la sala existe
                bool exists = await RoomManager.RoomExistsAsync(code);
                if (!exists)
                {
                    lblStatus.text = "⚠ Sala no encontrada. Verifica el código.";
                    btnConnect.interactable = true;
                    return;
                }
            }

            // Guardar para la escena de chat
            SelectedUsername = username;
            SelectedRoomCode = code;

            // Cargar escena según protocolo elegido
            SceneManager.LoadScene(UseTCP ? sceneTCP : sceneUDP);
        }
        catch (System.Exception ex)
        {
            lblStatus.text = $"Error de conexión: {ex.Message}";
            btnConnect.interactable = true;
        }
    }
}