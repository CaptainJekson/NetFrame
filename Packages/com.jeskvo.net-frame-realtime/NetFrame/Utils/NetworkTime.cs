using System.Runtime.CompilerServices;
using UnityEngine;

namespace NetFrame.Utils
{
    public static class NetworkTime
    {
#if UNITY_2020_3_OR_NEWER
        public static double LocalTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.timeAsDouble;
        }
#else
        // need stopwatch for older Unity versions, but it's quite slow.
        // CAREFUL: unlike Time.time, this is not a FRAME time.
        //          it changes during the frame too.
        static readonly Stopwatch stopwatch = new Stopwatch();
        static NetworkTime() => stopwatch.Start();
        public static double LocalTime => stopwatch.Elapsed.TotalSeconds;
#endif
    }
}