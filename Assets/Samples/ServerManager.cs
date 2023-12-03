using System.Collections.Generic;
using System.Reflection;
using NetFrame.Server;
using NetFrame.Utils;
using Samples.Dataframes;
using Samples.Dataframes.Collections;
using UnityEngine;

namespace Samples
{
    public class ServerManager : MonoBehaviour
    {
        private NetFrameServer _server;
        
        private void Start()
        {
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            _server = new NetFrameServer();
            
            _server.Start(8080, 10);

            _server.ClientConnection += OnClientConnection;
            _server.ClientDisconnect += OnClientDisconnect;
            
            _server.Subscribe<TestByteNetworkDataframe>(TestByteDataframeHandler);
            _server.Subscribe<TestNicknameDataframe>(TestNicknameDataframeHandler);
        }

        private void OnClientConnection(int id)
        {
            Debug.Log($"client connected Id = {id}");
        }
        
        private void OnClientDisconnect(int id)
        {
            Debug.Log($"client disconnected Id = {id}");
        }

        private void Update()
        {
            _server.Run();
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                var dataframe = new TestStringIntNetworkDataframe
                {
                    Name = "Vasya",
                    Age = 27,
                };
                _server.SendAll(ref dataframe);
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                var users = new List<UserNetworkModel>
                {
                    new()
                    {
                        FirstName = "Nataly",
                        LastName = "Prohorova",
                        Age = 24,
                        IsLeader = false,
                    },
                    new()
                    {
                        FirstName = "Evgeniy",
                        LastName = "Skvortsov",
                        Age = 32,
                        IsLeader = true,
                    },
                    new()
                    {
                        FirstName = "Oksana",
                        LastName = "Soskova",
                        Age = 27,
                        IsLeader = false,
                    }
                };
            
                for (var i = 0; i < 1500; i++) //todo эмуляция данных больше буффера
                {
                    users.Add(new UserNetworkModel
                    {
                        FirstName = "Move",
                        LastName = "User",
                        Age = 20,
                        IsLeader = false,
                    });
                }
                    
                var dataframeCollection = new UsersNetworkDataframe()
                {
                    Users = users,
                };
                _server.SendAll(ref dataframeCollection);
            }
        }
        
        private void TestByteDataframeHandler(TestByteNetworkDataframe networkDataframe, int id)
        {
            Debug.Log($"TestByteDataframe: client id = {id} | {networkDataframe.Value1} {networkDataframe.Value2} {networkDataframe.Value3}");
        }
        
        private void TestNicknameDataframeHandler(TestNicknameDataframe networkDataframe, int id)
        {
            Debug.Log($"TestNicknameDataframe: client id = {id} | nickname: {networkDataframe.Nickname}");
        }

        private void OnDestroy()
        {
            _server.ClientConnection -= OnClientConnection;
            _server.ClientDisconnect -= OnClientDisconnect;
            
            _server.Unsubscribe<TestByteNetworkDataframe>(TestByteDataframeHandler);
        }
        
        private void OnApplicationQuit()
        {
            _server.Stop();
        }
    }
}
