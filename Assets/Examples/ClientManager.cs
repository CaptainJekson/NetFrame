using System;
using System.Reflection;
using System.Threading;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Dataframes;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace Examples
{
    public class ClientManager : MonoBehaviour
    {
        private const string FilePath = "/RSAKeys/publicRSAKey.xml";
        
        private readonly string _ipAddress = "127.0.0.1";

        private NetFrameClient _netFrameClient;

        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _netFrameClient = new NetFrameClient(50000);

            string rsaXmlParameters = LoadRsaParameters();
            Debug.Log("Loaded rsa parameters: " + rsaXmlParameters);
            
            _netFrameClient.SetProtectionWithXml(rsaXmlParameters, "fk2kgb3kggl3jgl3nlg3g312");
            _netFrameClient.Connect(_ipAddress, 8080);

            _netFrameClient.ConnectionSuccessful += OnConnectionSuccessful;
            _netFrameClient.LogCall += OnLog;
            _netFrameClient.Disconnected += OnDisconnected;
            _netFrameClient.ConnectionFailed += OnConnectionFailed;
            
            _netFrameClient.Subscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _netFrameClient.Subscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _netFrameClient.Subscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _netFrameClient.Subscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
        }

        private void OnConnectionFailed()
        {
            Debug.LogError($"thread id = {Thread.CurrentThread.ManagedThreadId}");
        }

        private void Update()
        {
            _netFrameClient.Run(100);
            
            if (Input.GetKeyDown(KeyCode.E)) //Disconnect
            {
                _netFrameClient.Disconnect();
            }

            if (Input.GetKeyDown(KeyCode.R)) //Reconnection
            {
                _netFrameClient.Connect(_ipAddress, 8080);
            }
            
            if (Input.GetKeyDown(KeyCode.T)) //Send
            {
                var testByteDataframe = new TestByteNetworkDataframe
                {
                    Value1 = (byte) Random.Range(0,255),
                    Value2 = (byte) Random.Range(0,255),
                    Value3 = (byte) Random.Range(0,255),
                };
                _netFrameClient.Send(ref testByteDataframe);
            }
        }

        private void OnDisconnected()
        {
            Debug.Log("Disconnected from the server");
        }
        
        private void OnConnectionSuccessful(int localClientId)
        {
            var dataframe = new TestNicknameDataframe
            {
                Nickname = "Mega_nagibator",
            };
            //_client.Send(ref dataframe);
            Debug.Log($"Connected Successful to server, id {localClientId}");
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
            _netFrameClient.ConnectionSuccessful -= OnConnectionSuccessful;
            _netFrameClient.LogCall -= OnLog;
            _netFrameClient.Disconnected -= OnDisconnected;
            _netFrameClient.ConnectionFailed -= OnConnectionFailed;
            
            _netFrameClient.Unsubscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _netFrameClient.Unsubscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _netFrameClient.Unsubscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _netFrameClient.Unsubscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
        }

        private void OnApplicationQuit()
        {
            _netFrameClient.Disconnect();
        }

        private string LoadRsaParameters()
        {
            var request = UnityWebRequest.Get(Application.streamingAssetsPath + FilePath);
            request.SendWebRequest();

            while (!request.isDone)
            {
                if (!string.IsNullOrWhiteSpace(request.error))
                    throw new Exception("File load error");
            }

            string parameters = System.Text.Encoding.Default.GetString(request.downloadHandler.data);
            return parameters;
        }
    }
}
