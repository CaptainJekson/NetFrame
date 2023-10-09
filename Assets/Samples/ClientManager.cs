using System;
using NetFrame.Client;
using NetFrame.Enums;
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

            Client.ConnectionSuccessful += OnConnectionSuccessful;
            Client.ConnectedFailed += OnConnectedFailed;
            Client.Disconnected += OnDisconnected;
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectionSuccessful()
        {
            Debug.Log("Connected Successful to server");
        }
        
        private void OnConnectedFailed(ReasonServerConnectionFailed reason)
        {
            switch (reason)
            {
                case ReasonServerConnectionFailed.AlreadyConnected:
                    Debug.LogError("already connected");
                    break;
                case ReasonServerConnectionFailed.ImpossibleToConnect:
                    Debug.LogError("impossible to connect");
                    break;
                case ReasonServerConnectionFailed.ConnectionLost:
                    Debug.LogError("connection lost");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }

        private void Update()
        {
            Client.Run();
            
            if (Input.GetKeyDown(KeyCode.D)) //Disconnect
            {
                Client.Disconnect();
            }

            if (Input.GetKeyDown(KeyCode.C)) //Reconnection
            {
                Client.Connect("127.0.0.1", 8080);
            }
        }

        private void OnDestroy()
        {
            Client.ConnectionSuccessful -= OnConnectionSuccessful;
            Client.ConnectedFailed -= OnConnectedFailed;
            Client.Disconnected -= OnDisconnected;
        }

        private void OnApplicationQuit()
        {
            Client.Disconnect();
        }
    }
}
