using NetFrame.Server;
using NetFrame.Utils;
using UnityEngine;

namespace Samples
{
    public class ServerManager : MonoBehaviour
    {
        private NetFrameServer _server;
        private DatagramsGenerator _datagramsGenerator;
        
        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            _server = new NetFrameServer();
            
            _datagramsGenerator.Run();
            _server.Start(8080, 10);

            _server.ClientConnection += OnClientConnection;
            _server.ClientDisconnect += OnClientDisconnect;
        }

        private void OnClientConnection(int id)
        {
            Debug.Log($"client connected Id = {id}");
        }
        
        private void OnClientDisconnect(int id)
        {
            Debug.Log($"client disconnected Id = {id}");
        }

        private void Update()
        {
            _server.Run();
        }

        private void OnDestroy()
        {
            _server.ClientConnection -= OnClientConnection;
            _server.ClientDisconnect -= OnClientDisconnect;
        }
    }
}
