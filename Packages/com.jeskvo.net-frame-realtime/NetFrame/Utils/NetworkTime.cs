using System.Runtime.CompilerServices;
using UnityEngine;

namespace NetFrame.Utils
{
    public static class NetworkTime
    {
        public static double LocalTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.timeAsDouble;
        }
    }
}