using System;
using NetFrame.ZOld.Enums;

namespace NetFrame.ZOld.ThreadSafeContainers
{
    [Obsolete]
    public class ConnectedFailedSafeContainer
    {
        public ReasonServerConnectionFailed Reason;
        public string Parameters;
    }
}