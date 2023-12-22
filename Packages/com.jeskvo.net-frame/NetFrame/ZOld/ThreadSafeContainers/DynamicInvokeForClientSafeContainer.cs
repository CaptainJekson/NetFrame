using System;
using System.Collections.Generic;

namespace NetFrame.ZOld.ThreadSafeContainers
{
    [Obsolete]
    public class DynamicInvokeForClientSafeContainer
    {
        public List<Delegate> Handlers;
        public INetworkDataframe Dataframe;
    }
}