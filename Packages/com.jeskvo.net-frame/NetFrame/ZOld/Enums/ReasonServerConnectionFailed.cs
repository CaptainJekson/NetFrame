using System;

namespace NetFrame.ZOld.Enums
{
    [Obsolete]
    public enum ReasonServerConnectionFailed : byte
    {
        AlreadyConnected = 0,
        ImpossibleToConnect = 1,
        ConnectionLost = 2,
        NoDataframe = 3,
    }
}