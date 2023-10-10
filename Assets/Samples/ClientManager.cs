using NetFrame.Client;
using NetFrame.Enums;
using NetFrame.Utils;
using Samples.Datagrams;
using Samples.Datagrams.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Samples
{
    public class ClientManager : MonoBehaviour
    {
        private NetFrameClient _client;
        
        private DatagramsGenerator _datagramsGenerator;

        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            _client = new NetFrameClient();
            
            _datagramsGenerator.Run();
            _client.Connect("127.0.0.1", 8080);

            _client.ConnectionSuccessful += OnConnectionSuccessful;
            _client.ConnectedFailed += OnConnectedFailed;
            _client.Disconnected += OnDisconnected;
            
            _client.Subscribe<TestStringIntDatagram>(TestByteDatagramHandler);
            _client.Subscribe<UsersDatagram>(UsersDatagramHandler);
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
                var testByteDatagram = new TestByteDatagram
                {
                    Value1 = (byte) Random.Range(0,255),
                    Value2 = (byte) Random.Range(0,255),
                    Value3 = (byte) Random.Range(0,255),
                };
                _client.Send(ref testByteDatagram);
            }
        }
        
        private void TestByteDatagramHandler(TestStringIntDatagram datagram)
        {
            Debug.Log($"TestByteDatagram: {datagram.Name} {datagram.Age}");
        }
        
        private void UsersDatagramHandler(UsersDatagram datagram)
        {
            Debug.Log($"TestByteDatagram users count: {datagram.Users.Count}");
            foreach (var user in datagram.Users)
            {
                Debug.Log($"First Name: {user.FirstName} | Last Name: {user.LastName} | Age: {user.Age} | Is Leader {user.IsLeader}");
            }
        }

        private void OnDestroy()
        {
            _client.ConnectionSuccessful -= OnConnectionSuccessful;
            _client.ConnectedFailed -= OnConnectedFailed;
            _client.Disconnected -= OnDisconnected;
            
            _client.Unsubscribe<TestStringIntDatagram>(TestByteDatagramHandler);
            _client.Unsubscribe<UsersDatagram>(UsersDatagramHandler);
        }

        private void OnApplicationQuit()
        {
            _client.Disconnect();
        }
    }
}
