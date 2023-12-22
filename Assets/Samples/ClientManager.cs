using System.Reflection;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Dataframes;
using Samples.Units;
using UnityEngine;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        private string _ipAddress = "192.168.31.103"; //"127.0.0.1"
        
        public static ClientManager Instance;
        
        [SerializeField] private Player player;
        
        private NetFrameClient _netFrameClient;

        private void Start()
        {
            Instance = this;
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _netFrameClient = new NetFrameClient(50000);
            
            _netFrameClient.Connect(_ipAddress, 8080);

            _netFrameClient.ConnectionSuccessful += OnConnectionSuccessful;
            _netFrameClient.LogCall += OnLog;
            _netFrameClient.Disconnected += OnDisconnected;
            
            _netFrameClient.Subscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _netFrameClient.Subscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _netFrameClient.Subscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _netFrameClient.Subscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
            _netFrameClient.Subscribe<PlayerMoveDataframe>(PlayerMoveDataframeHandler);
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

            if (Input.GetKeyDown(KeyCode.U))
            {
                _netFrameClient.SendTestUdp("Hello from UDP!!!");
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
        
        private void PlayerMoveDataframeHandler(PlayerMoveDataframe dataframe)
        {
            player.NetFrameTransform.AddNetworkDataframeTransform(dataframe);
        }

        private void OnDestroy()
        {
            _netFrameClient.ConnectionSuccessful -= OnConnectionSuccessful;
            _netFrameClient.LogCall -= OnLog;
            _netFrameClient.Disconnected -= OnDisconnected;
            
            _netFrameClient.Unsubscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _netFrameClient.Unsubscribe<UsersNetworkDataframe>(UsersDataframeHandler);
            _netFrameClient.Unsubscribe<TestClientConnectedDataframe>(TestClientConnectedDataframeHandler);
            _netFrameClient.Unsubscribe<TestClientDisconnectDataframe>(TestClientDisconnectDataframeHandler);
        }

        private void OnApplicationQuit()
        {
            _netFrameClient.Disconnect();
        }
    }
}
