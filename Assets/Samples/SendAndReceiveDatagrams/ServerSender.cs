using Samples.Datagrams;
using UnityEngine;

namespace Samples.SendAndReceiveDatagrams
{
    public class ServerSender : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                var datagram = new TestStringIntDatagram
                {
                    Name = "Vasya",
                    Age = 27,
                };
                ServerManager.Instance.Server.SendAll(ref datagram);
            }
        }
    }
}