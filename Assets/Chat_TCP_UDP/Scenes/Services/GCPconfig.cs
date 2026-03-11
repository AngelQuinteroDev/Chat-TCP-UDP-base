public static class GCPConfig
{

    public const string SERVER_IP  = "34.59.37.232";

    public const int TCP_PORT  = 9000;   
    public const int UDP_PORT  = 9001;  
    public const int REST_PORT = 5000;   

    public static string API_URL => $"http://{SERVER_IP}:{REST_PORT}";
}