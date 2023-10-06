using Samples.Datagrams;
using UnityEngine;

namespace Samples.SendAndReceiveDatagrams
{
    public class ServerReceiver : MonoBehaviour
    {
        private void Start()
        {
            ServerManager.Instance.Server.Subscribe<TestByteDatagram>(TestByteDatagramHandler);
        }

        private void TestByteDatagramHandler(TestByteDatagram datagram, int id)
        {
            Debug.Log($"TestByteDatagram: {datagram.Value1} {datagram.Value2} {datagram.Value3}");
        }

        private void OnDestroy()
        {
            ServerManager.Instance.Server.Unsubscribe<TestByteDatagram>(TestByteDatagramHandler);
        }
    }
}