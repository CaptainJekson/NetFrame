using System.Collections.Generic;
using NetFrame.Server;
using NetFrame.Utils;
using Samples.Datagrams;
using Samples.Datagrams.Collections;
using UnityEngine;

namespace Samples
{
    public class ServerManager : MonoBehaviour
    {
        private NetFrameServer _server;
        
        private DatagramsGenerator _datagramsGenerator;
        
        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            _server = new NetFrameServer();
            
            _datagramsGenerator.Run();
            _server.Start(8080, 10);

            _server.ClientConnection += OnClientConnection;
            _server.ClientDisconnect += OnClientDisconnect;
            
            _server.Subscribe<TestByteDatagram>(TestByteDatagramHandler);
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
                var datagram = new TestStringIntDatagram
                {
                    Name = "Vasya",
                    Age = 27,
                };
                _server.SendAll(ref datagram);
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
                    
                var datagramCollection = new UsersDatagram()
                {
                    Users = users,
                };
                _server.SendAll(ref datagramCollection);
            }
        }
        
        private void TestByteDatagramHandler(TestByteDatagram datagram, int id)
        {
            Debug.Log($"TestByteDatagram: {datagram.Value1} {datagram.Value2} {datagram.Value3}");
        }

        private void OnDestroy()
        {
            _server.ClientConnection -= OnClientConnection;
            _server.ClientDisconnect -= OnClientDisconnect;
            
            _server.Unsubscribe<TestByteDatagram>(TestByteDatagramHandler);
        }
        
        private void OnApplicationQuit()
        {
            _server.Stop();
        }
    }
}
