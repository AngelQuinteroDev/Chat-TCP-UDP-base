using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class UDPClient : MonoBehaviour, IClient
{
    private UdpClient  udpClient;
    private IPEndPoint remoteEndPoint;
    public  bool       isServerConnected = false;

    public event Action<string> OnMessageReceived;
    public event Action         OnConnected;
    public event Action         OnDisconnected;

    public bool isConnected { get; private set; }

    public async Task ConnectToServer(string ipAddress, int port)
    {
        udpClient      = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        isConnected = true;
        _ = ReceiveLoop();

        await SendMessageAsync("CONNECT"); // Mensaje inicial identico al original del profe
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (isConnected)
            {
                UdpReceiveResult result  = await udpClient.ReceiveAsync();
                string           message = Encoding.UTF8.GetString(result.Buffer).Trim(); // Trim elimina el \n del servidor

                // El servidor GCP manda {"type":"CONNECTED",...} en vez de "CONNECTED" plano
                // Aceptamos ambos para mantener compatibilidad con el servidor del profe
                if (message == "CONNECTED" || message.Contains("\"type\":\"CONNECTED\""))
                {
                    Debug.Log("[Client] Server Answered");
                    OnConnected?.Invoke();
                    continue;
                }

                Debug.Log("[Client] Received: " + message.Substring(0, Mathf.Min(120, message.Length)));
                OnMessageReceived?.Invoke(message);
            }
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isConnected)
        {
            Debug.Log("[Client] Not connected to server.");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);
        await udpClient.SendAsync(data, data.Length, remoteEndPoint);

        Debug.Log("[Client] Sent: " + message.Substring(0, Mathf.Min(120, message.Length)));
    }

    public void Disconnect()
    {
        if (!isConnected)
        {
            Debug.Log("[Client] The client is not connected");
            return;
        }

        isConnected = false;
        udpClient?.Close();
        udpClient?.Dispose();
        udpClient = null;

        Debug.Log("[Client] Disconnected");
        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}