using System;
using System.Collections.Generic;

namespace NetFrame.ThreadSafeContainers
{
    public class DynamicInvokeForClientSafeContainer
    {
        public List<Delegate> Handlers;
        public INetworkDataframe Dataframe;
    }
}