using System;
using System.Reflection;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Dataframes;
using UnityEngine;

namespace Samples
{
    public class ClientManager2 : MonoBehaviour
    {
        private NetFrameClient _netFrameClient;
        
        private void Awake()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _netFrameClient = new NetFrameClient(2000);
            
            _netFrameClient.Connect("127.0.0.1", 8080);
            
            
            _netFrameClient.ConnectionSuccessful += OnConnectionSuccessful;
            _netFrameClient.LogCall += OnLog;
            _netFrameClient.Disconnected += OnDisconnected;
            
            _netFrameClient.Subscribe<TestForMaximDataframe>(TestForMaximDataframeHandler);
        }

        private void TestForMaximDataframeHandler(TestForMaximDataframe dataframe)
        {
            Debug.LogError($"name {dataframe.Name} age{dataframe.Age}");
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectionSuccessful()
        {
            Debug.Log("Connected Successful to server");
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

        private void Update()
        {
            _netFrameClient.Run(100);
        }

        private void OnApplicationQuit()
        {
            _netFrameClient.ConnectionSuccessful -= OnConnectionSuccessful;
            _netFrameClient.LogCall -= OnLog;
            _netFrameClient.Disconnected -= OnDisconnected;
            
            _netFrameClient.Unsubscribe<TestForMaximDataframe>(TestForMaximDataframeHandler);
            
            _netFrameClient.Disconnect();
        }
    }
}