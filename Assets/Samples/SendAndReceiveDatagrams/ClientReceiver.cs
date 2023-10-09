using Samples.Datagrams;
using UnityEngine;

namespace Samples.SendAndReceiveDatagrams
{
    public class ClientReceiver : MonoBehaviour
    {
        private void Start()
        {
            ClientManager.Instance.Client.Subscribe<TestStringIntDatagram>(TestByteDatagramHandler);
        }

        private void TestByteDatagramHandler(TestStringIntDatagram datagram)
        {
            Debug.Log($"TestByteDatagram: {datagram.Name} {datagram.Age}");
        }

        private void OnDestroy()
        {
            ClientManager.Instance.Client.Unsubscribe<TestStringIntDatagram>(TestByteDatagramHandler);
        }
    }
}