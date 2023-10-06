using System;
using Samples.Datagrams;
using UnityEngine;

namespace Samples.SendAndReceiveDatagrams
{
    public class ClientSender : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                var testByteDatagram = new TestByteDatagram
                {
                    Value1 = 24,
                    Value2 = 251,
                    Value3 = 8,
                };
                ClientManager.Instance.Client.Send(ref testByteDatagram);
            }
        }
    }
}