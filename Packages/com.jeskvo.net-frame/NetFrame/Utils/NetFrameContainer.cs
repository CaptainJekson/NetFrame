using NetFrame.Client;
using NetFrame.Server;

namespace NetFrame.Utils
{
    public static class NetFrameContainer
    {
        public static NetFrameClient NetFrameClient { get; private set; }
        public static NetFrameServer NetFrameServer { get; private set; }
        
        internal static void SetClient(NetFrameClient netFrameClient)
        {
            NetFrameClient = netFrameClient;
        }

        internal static void SetServer(NetFrameServer netFrameServer)
        {
            NetFrameServer = netFrameServer;
        }
    }
}