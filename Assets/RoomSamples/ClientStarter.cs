using NetFrame.Client;
using UnityEngine;

public class ClientStarter : MonoBehaviour
{
    private NetFrameClient _client;

    private void Start()
    {
        _client = new NetFrameClient(2000);
        _client.Connect("127.0.0.1", 8080);

        _client.ConnectionSuccessful += OnConnectionSuccessful;
        _client.Disconnected += OnDisconnected;
    }

    private void Update()
    {
        _client.Run(25);
    }

    private void OnApplicationQuit()
    {
        _client.ConnectionSuccessful -= OnConnectionSuccessful;
        _client.Disconnected -= OnDisconnected;
    }

    private void OnConnectionSuccessful()
    {
        Debug.Log("CONNECTED");
    }

    private void OnDisconnected()
    {
        Debug.Log("DISCONNECTED");
    }
}