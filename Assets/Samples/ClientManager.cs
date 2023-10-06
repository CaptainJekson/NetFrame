using NetFrame.Client;
using NetFrame.Utils;
using UnityEngine;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        private NetFrameClient _client;
        private DatagramsGenerator _datagramsGenerator;

        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            _client = new NetFrameClient();
            
            _datagramsGenerator.Run();
            _client.Connect("127.0.0.1", 8080);

            _client.Disconnected += OnDisconnected;
            _client.ConnectedFailed += OnConnectedFailed;
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectedFailed(string message)
        {
            Debug.LogError($"error connecting to server {message}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
            {
                _client.Disconnect();
            }
        }

        private void OnDestroy()
        {
            _client.Disconnected -= OnDisconnected;
            _client.ConnectedFailed -= OnConnectedFailed;
        }
    }
}
