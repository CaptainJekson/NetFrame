using System;
using System.Reflection;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Dataframes;
using UnityEngine;
using LogType = NetFrame.Enums.LogType;
using Random = UnityEngine.Random;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        private Client _client;

        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _client = new Client(50000);
            
            _client.Connect("127.0.0.1", 8080);

            _client.ConnectionSuccessful += OnConnectionSuccessful;
            _client.LogCall += OnLog;
            _client.Disconnected += OnDisconnected;
            
            _client.Subscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _client.Subscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _client.Subscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _client.Subscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
        }
        
        private void Update()
        {
            _client.Run(100);
            
            if (Input.GetKeyDown(KeyCode.D)) //Disconnect
            {
                _client.Disconnect();
            }

            if (Input.GetKeyDown(KeyCode.C)) //Reconnection
            {
                _client.Connect("127.0.0.1", 8080);
            }
            
            if (Input.GetKeyDown(KeyCode.S)) //Send
            {
                var testByteDataframe = new TestByteNetworkDataframe
                {
                    Value1 = (byte) Random.Range(0,255),
                    Value2 = (byte) Random.Range(0,255),
                    Value3 = (byte) Random.Range(0,255),
                };
                _client.Send(ref testByteDataframe);
            }
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectionSuccessful()
        {
            var dataframe = new TestNicknameDataframe
            {
                Nickname = "Mega_nagibator",
            };
            //_client.Send(ref dataframe);
            Debug.Log("Connected Successful to server");
        }
        
        private void OnLog(LogType reason, string value)
        {
            switch (reason)
            {
                case LogType.Info:
                    Debug.Log(value);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(value);
                    break;
                case LogType.Error:
                    Debug.LogError(value);
                    break;
            }
        }

        private void TestByteDataframeHandler(TestStringIntNetworkDataframe networkDataframe)
        {
            Debug.Log($"TestByteDataframe: {networkDataframe.Name} {networkDataframe.Age}");
        }
        
        private void UsersDataframeHandler(UsersNetworkDataframe networkDataframe)
        {
            Debug.Log($"TestByteDataframe users count: {networkDataframe.Users.Count}");
            foreach (var user in networkDataframe.Users)
            {
                Debug.Log($"First Name: {user.FirstName} | Last Name: {user.LastName} | Age: {user.Age} | Is Leader {user.IsLeader}");
            }
        }
        
        private void TestClientConnectedDataframeHandler(TestClientConnectedDataframe dataframe)
        {
            Debug.LogError($"Client Connected to server ---> {dataframe.ClientId}");
        }
        
        private void TestClientDisconnectDataframeHandler(TestClientDisconnectDataframe dataframe)
        {
            Debug.LogError($"Client Disconnect to server ---> {dataframe.ClientId}");
        }

        private void OnDestroy()
        {
            _client.ConnectionSuccessful -= OnConnectionSuccessful;
            _client.LogCall -= OnLog;
            _client.Disconnected -= OnDisconnected;
            
            _client.Unsubscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _client.Unsubscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _client.Unsubscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _client.Unsubscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
        }

        private void OnApplicationQuit()
        {
            _client.Disconnect();
        }
    }
}
