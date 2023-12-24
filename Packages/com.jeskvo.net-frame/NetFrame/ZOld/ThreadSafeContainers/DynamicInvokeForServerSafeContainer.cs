using System;
using System.Collections.Generic;

namespace NetFrame.ZOld.ThreadSafeContainers
{
    [Obsolete]
    public class DynamicInvokeForServerSafeContainer
    {
        public List<Delegate> Handlers;
        public INetworkDataframe Dataframe;
        public int Id;
    }
}