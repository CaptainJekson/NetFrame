using System.Reflection;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.DataframesForRealtime;
using Samples.Units;
using UnityEngine;

namespace Samples
{
    public class ClientRealtimeManager : MonoBehaviour
    {
        [SerializeField] private NetworkTransformPlayer localPlayerTemplate;
        [SerializeField] private NetworkTransformPlayer remotePlayerTemplate;
        
        private string _ipAddress = "192.168.31.103"; //"127.0.0.1"

        private NetFrameClient _netFrameClient;
        
        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _netFrameClient = new NetFrameClient(2000);
            _netFrameClient.Connect(_ipAddress, 8080);

            _netFrameClient.ConnectionSuccessful += OnConnectionSuccessful;
            _netFrameClient.LogCall += OnLog;
            _netFrameClient.Disconnected += OnDisconnected;
            
            _netFrameClient.Subscribe<PlayerSpawnDataframe>(PlayerSpawnDataframeHandler);
            
            Instantiate(localPlayerTemplate, Vector3.zero, Quaternion.identity);
        }

        private void Update()
        {
            _netFrameClient.Run(100);
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectionSuccessful()
        {
            Debug.Log("Connected Successful to server");

            var startPosition = new Vector3(Random.Range(-10f, 10f), Random.Range(-4f, 4f),0);
            var startRotation = Quaternion.identity;
            
            var spawnedPlayer = Instantiate(localPlayerTemplate, startPosition, startRotation);

            var spawnDataframe = new PlayerSpawnDataframe
            {
                StartPosition = startPosition,
                StartRotation = startRotation,
            };
            _netFrameClient.Send(ref spawnDataframe);
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
        
        private void PlayerSpawnDataframeHandler(PlayerSpawnDataframe dataframe)
        {
            var startPosition = dataframe.StartPosition;
            var startRotation = dataframe.StartRotation;
            
            var spawnedPlayer = Instantiate(remotePlayerTemplate, startPosition, startRotation);
        }

        private void OnApplicationQuit()
        {
            _netFrameClient.ConnectionSuccessful -= OnConnectionSuccessful;
            _netFrameClient.LogCall -= OnLog;
            _netFrameClient.Disconnected -= OnDisconnected;
            
            _netFrameClient.Unsubscribe<PlayerSpawnDataframe>(PlayerSpawnDataframeHandler);
            
            _netFrameClient.Disconnect();
        }
    }
}