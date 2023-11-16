using System;
using System.Collections.Generic;

namespace NetFrame.ThreadSafeContainers
{
    public class DynamicInvokeForServerSafeContainer
    {
        public List<Delegate> Handlers;
        public INetworkDataframe Dataframe;
        public int Id;
    }
}