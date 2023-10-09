using NetFrame.Server;
using NetFrame.Utils;
using Samples.Datagrams;
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
            
            if (Input.GetKeyDown(KeyCode.S)) //Send
            {
                var datagram = new TestStringIntDatagram
                {
                    Name = "Vasya",
                    Age = 27,
                };
                Server.SendAll(ref datagram);
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
