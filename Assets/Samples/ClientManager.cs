using NetFrame.Client;
using NetFrame.Utils;
using UnityEngine;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        public static ClientManager Instance;
        public NetFrameClient Client;
        
        private DatagramsGenerator _datagramsGenerator;

        private void Start()
        {
            Instance = this;
            
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            Client = new NetFrameClient();
            
            _datagramsGenerator.Run();
            Client.Connect("127.0.0.1", 8080);

            Client.Disconnected += OnDisconnected;
            Client.ConnectedFailed += OnConnectedFailed; //todo не работает
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
                Client.Disconnect();
            }
        }

        private void OnDestroy()
        {
            Client.Disconnected -= OnDisconnected;
            Client.ConnectedFailed -= OnConnectedFailed;
        }

        private void OnApplicationQuit()
        {
            Client.Disconnect();
        }
    }
}
