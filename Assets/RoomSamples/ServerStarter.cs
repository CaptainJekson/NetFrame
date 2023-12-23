using NetFrame.Server;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    private NetFrameServer _server;
    
    private void Start()
    {
        _server = new NetFrameServer(2000);
        _server.Start(8080, 10);

        _server.ClientConnection += OnClientConnected;
        _server.ClientDisconnect += OnClientDisconnected;
    }

    private void Update()
    {
        _server.Run(25);
    }
    
    private void OnApplicationQuit()
    {
        _server.ClientConnection -= OnClientConnected;
        _server.ClientDisconnect -= OnClientDisconnected;
        
        _server.Stop();
    }

    private void OnClientConnected(int clientId)
    {
        Debug.Log($"CLIENT_{clientId}_CONNECTED");
    }

    private void OnClientDisconnected(int clientId)
    {
        Debug.Log($"CLIENT_{clientId}_DISCONNECTED");
    }
}