using NetFrame.Server;
using NetFrame.Utils;
using UnityEngine;

namespace Samples
{
    public class ServerManager : MonoBehaviour
    {
        public static ServerManager Instance;
        public NetFrameServer Server;
        
        private DatagramsGenerator _datagramsGenerator;
        
        private void Start()
        {
            Instance = this;
            
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            Server = new NetFrameServer();
            
            _datagramsGenerator.Run();
            Server.Start(8080, 10);

            Server.ClientConnection += OnClientConnection;
            Server.ClientDisconnect += OnClientDisconnect;
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
            Server.Run();
        }

        private void OnDestroy()
        {
            Server.ClientConnection -= OnClientConnection;
            Server.ClientDisconnect -= OnClientDisconnect;
        }
    }
}
