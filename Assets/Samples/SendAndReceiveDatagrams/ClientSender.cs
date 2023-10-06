using System;
using Samples.Datagrams;
using UnityEngine;
using Random = UnityEngine.Random;

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
                    Value1 = (byte) Random.Range(0,255),
                    Value2 = (byte) Random.Range(0,255),
                    Value3 = (byte) Random.Range(0,255),
                };
                ClientManager.Instance.Client.Send(ref testByteDatagram);
            }
        }
    }
}