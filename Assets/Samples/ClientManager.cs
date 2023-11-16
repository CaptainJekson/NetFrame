using System.Reflection;
using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Dataframes;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        private NetFrameClient _client;

        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            
            _client = new NetFrameClient();
            
            _client.Connect("127.0.0.1", 8080);

            _client.ConnectionSuccessful += OnConnectionSuccessful;
            _client.ConnectedFailed += OnConnectedFailed;
            _client.Disconnected += OnDisconnected;
            
            _client.Subscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _client.Subscribe<UsersNetworkDataframe>(UsersDataframeHandler);
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
            _client.Send(ref dataframe);
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
            }
        }

        private void Update()
        {
            _client.Run();
            
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

        private void OnDestroy()
        {
            _client.ConnectionSuccessful -= OnConnectionSuccessful;
            _client.ConnectedFailed -= OnConnectedFailed;
            _client.Disconnected -= OnDisconnected;
            
            _client.Unsubscribe<TestStringIntNetworkDataframe>(TestByteDataframeHandler);
            _client.Unsubscribe<UsersNetworkDataframe>(UsersDataframeHandler);
        }

        private void OnApplicationQuit()
        {
            _client.Disconnect();
        }
    }
}
