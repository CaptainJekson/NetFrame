using System.Reflection;
using NetFrame.Enums;
using NetFrame.Server;
using NetFrame.Utils;
using Samples.DataframesForRealtime;
using UnityEngine;

namespace Samples
{
    public class ServerRealTimeManager : MonoBehaviour
    {
        private NetFrameServer _netFrameServer;
        
        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            _netFrameServer = new NetFrameServer(2000);
            
            _netFrameServer.Start(8080, 10);

            _netFrameServer.ClientConnection += OnClientConnection;
            _netFrameServer.ClientDisconnect += OnClientDisconnect;
            _netFrameServer.LogCall += OnLog;
            
            _netFrameServer.Subscribe<PlayerSpawnDataframe>(PlayerSpawnRemoteRequestDataframeHandler);
        }

        private void Update()
        {
            _netFrameServer.Run(100);
        }

        private void PlayerSpawnRemoteRequestDataframeHandler(PlayerSpawnDataframe dataframe, int id)
        {
            _netFrameServer.SendAllExcept(ref dataframe, id);
        }

        private void OnClientConnection(int id)
        {
            Debug.Log($"client connected Id = {id}");
        }
        
        private void OnClientDisconnect(int id)
        {
            Debug.Log($"client disconnected Id = {id}");
        }
        
        private void OnLog(NetworkLogType reason, string value)
        {
            switch (reason)
            {
                case NetworkLogType.Info:
                    Debug.Log(value);
                    break;
                case NetworkLogType.Warning:
                    Debug.LogWarning(value);
                    break;
                case NetworkLogType.Error:
                    Debug.LogError(value);
                    break;
            }
        }

        private void OnApplicationQuit()
        {
            _netFrameServer.Unsubscribe<PlayerSpawnDataframe>(PlayerSpawnRemoteRequestDataframeHandler);
        }
    }
}