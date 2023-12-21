using System.Collections.Generic;
using System.Reflection;
using NetFrame.Enums;
using NetFrame.Server;
using NetFrame.Utils;
using Samples.Dataframes;
using Samples.Dataframes.Collections;
using UnityEngine;

namespace Samples
{
    public class ServerManager : MonoBehaviour
    {
        public static ServerManager Instance;
        
        private NetFrameServer _netFrameServer;

        public NetFrameServer NetFrameServer => _netFrameServer;
        
        private void Start()
        {
            Instance = this;
            
            NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
            _netFrameServer = new NetFrameServer(50000);
            
            _netFrameServer.Start(8080, 10);

            _netFrameServer.ClientConnection += OnClientConnection;
            _netFrameServer.ClientDisconnect += OnClientDisconnect;
            _netFrameServer.LogCall += OnLog;
            
            _netFrameServer.Subscribe<TestByteNetworkDataframe>(TestByteDataframeHandler);
            _netFrameServer.Subscribe<TestNicknameDataframe>(TestNicknameDataframeHandler);
        }

        private void Update()
        {
            _netFrameServer.Run(100);
            
            if (Input.GetKeyDown(KeyCode.T))
            {
                var dataframe = new TestStringIntNetworkDataframe
                {
                    Name = "Vasya",
                    Age = 27,
                };
                _netFrameServer.SendAll(ref dataframe);
            }

            if (Input.GetKeyDown(KeyCode.Y))
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
                _netFrameServer.SendAll(ref dataframeCollection);
            }
        }
        
        private void OnClientConnection(int id)
        {
            Debug.Log($"client connected Id = {id}");
            
            //_netFrameServer.ShowRemoteEndPoint(id); //todo test
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
            _netFrameServer.ClientConnection -= OnClientConnection;
            _netFrameServer.ClientDisconnect -= OnClientDisconnect;
            _netFrameServer.LogCall -= OnLog;
            
            _netFrameServer.Unsubscribe<TestByteNetworkDataframe>(TestByteDataframeHandler);
        }

        private void OnApplicationQuit()
        {
            _netFrameServer.Stop();
        }
    }
}
