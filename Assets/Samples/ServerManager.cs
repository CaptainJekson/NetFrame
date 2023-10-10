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
        public NetFrameServer Server;
        
        private DatagramsGenerator _datagramsGenerator;
        
        private void Start()
        {
            _datagramsGenerator = new DatagramsGenerator(Application.dataPath);
            Server = new NetFrameServer();
            
            _datagramsGenerator.Run();
            Server.Start(8080, 10);

            Server.ClientConnection += OnClientConnection;
            Server.ClientDisconnect += OnClientDisconnect;
            
            Server.Subscribe<TestByteDatagram>(TestByteDatagramHandler);
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
            Server.Run();
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                var datagram = new TestStringIntDatagram
                {
                    Name = "Vasya",
                    Age = 27,
                };
                Server.SendAll(ref datagram);

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
                Server.SendAll(ref datagramCollection);
            }
        }
        
        private void TestByteDatagramHandler(TestByteDatagram datagram, int id)
        {
            Debug.Log($"TestByteDatagram: {datagram.Value1} {datagram.Value2} {datagram.Value3}");
        }

        private void OnDestroy()
        {
            Server.ClientConnection -= OnClientConnection;
            Server.ClientDisconnect -= OnClientDisconnect;
            
            Server.Unsubscribe<TestByteDatagram>(TestByteDatagramHandler);
        }
        
        private void OnApplicationQuit()
        {
            Server.Stop();
        }
    }
}
