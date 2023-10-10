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
        public NetFrameClient Client;
        
        private DatagramsGenerator _datagramsGenerator;

        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            Client = new NetFrameClient();
            
            _datagramsGenerator.Run();
            Client.Connect("127.0.0.1", 8080);

            Client.ConnectionSuccessful += OnConnectionSuccessful;
            Client.ConnectedFailed += OnConnectedFailed;
            Client.Disconnected += OnDisconnected;
            
            Client.Subscribe<TestStringIntDatagram>(TestByteDatagramHandler);
            Client.Subscribe<UsersDatagram>(UsersDatagramHandler);
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
            Client.Run();
            
            if (Input.GetKeyDown(KeyCode.D)) //Disconnect
            {
                Client.Disconnect();
            }

            if (Input.GetKeyDown(KeyCode.C)) //Reconnection
            {
                Client.Connect("127.0.0.1", 8080);
            }
            
            if (Input.GetKeyDown(KeyCode.S)) //Send
            {
                var testByteDatagram = new TestByteDatagram
                {
                    Value1 = (byte) Random.Range(0,255),
                    Value2 = (byte) Random.Range(0,255),
                    Value3 = (byte) Random.Range(0,255),
                };
                Client.Send(ref testByteDatagram);
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
            Client.ConnectionSuccessful -= OnConnectionSuccessful;
            Client.ConnectedFailed -= OnConnectedFailed;
            Client.Disconnected -= OnDisconnected;
            
            Client.Unsubscribe<TestStringIntDatagram>(TestByteDatagramHandler);
            Client.Unsubscribe<UsersDatagram>(UsersDatagramHandler);
        }

        private void OnApplicationQuit()
        {
            Client.Disconnect();
        }
    }
}
