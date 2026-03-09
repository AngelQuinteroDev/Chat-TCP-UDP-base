using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class TCPClient : MonoBehaviour, IClient
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;

    public bool isConnected { get; private set; }

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public async Task ConnectToServer(string ip, int port)
    {
        tcpClient = new TcpClient(); //Creates a new instance of the TcpClient class

        await tcpClient.ConnectAsync(ip, port); //Asynchronously connects to the server at the specified IP address and port number
        networkStream = tcpClient.GetStream();// Retrieves the network stream associated with the connected TCP client

        isConnected = true;
        Debug.Log("[Client] Connected to server");
        OnConnected?.Invoke(); // Invokes the OnConnected event, notifying any subscribed listeners that the client has successfully connected to the server

        _ = ReceiveLoop(); //Starts the receive loop in a separate task to continuously listen for incoming messages from the server without blocking the main thread
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[65536]; // Buffer to store incoming data from the server (64 KB to handle large messages like images)
        System.Text.StringBuilder accumulator = new System.Text.StringBuilder(); // Accumulates partial TCP chunks into complete messages

        try
        {
            while (tcpClient != null && tcpClient.Connected) // Continuously checks if the client is still connected to the server
            {
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length); // Reads data from the network stream asynchronously and stores it in the buffer, returning the number of bytes read
                if (bytesRead == 0) // If the server is disconnected, ReadAsync returns 0 bytes read
                {
                    Debug.Log("[Client] Server disconnected");
                    break;
                }
                accumulator.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead)); // Append chunk to accumulator

                // Process all complete messages (delimited by \n)
                string accumulated = accumulator.ToString();
                int newlineIndex;
                while ((newlineIndex = accumulated.IndexOf('\n')) >= 0)
                {
                    string message = accumulated.Substring(0, newlineIndex).Trim(); // Extract one complete message
                    accumulated = accumulated.Substring(newlineIndex + 1);          // Keep the remainder
                    if (!string.IsNullOrEmpty(message))
                    {
                        OnMessageReceived?.Invoke(message); // Invokes the OnMessageReceived event with the complete message
                        Debug.Log("[Client] Received from server: " + message.Substring(0, Mathf.Min(120, message.Length)));
                    }
                }
                accumulator.Clear();
                accumulator.Append(accumulated); // Keep any incomplete trailing data for next read
            }
        }
        finally
        {
            Disconnect();
        }
    }
    public async Task SendMessageAsync(string message)
    {
        if (!isConnected || networkStream == null) // Checks if there is an active connection to the server before attempting to send a message
        {
            Debug.Log("[Client] Not connected to server");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message + "\n");// Converts the message string into a byte array using UTF-8 encoding (\n required by server ReadLineAsync)
        await networkStream.WriteAsync(data, 0, data.Length);// Writes the byte array to the network stream asynchronously, sending it to the connected server

        Debug.Log("[Client] Sent: " + message);
    }

    public void Disconnect()// Closes the connection to the server and cleans
    {
        isConnected = false;

        networkStream?.Close();
        tcpClient?.Close();

        networkStream = null;
        tcpClient = null;

        OnDisconnected?.Invoke(); // Invokes the OnDisconnected event, notifying any subscribed listeners that the client has disconnected from the server
        Debug.Log("[Client] Disconnected");
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}