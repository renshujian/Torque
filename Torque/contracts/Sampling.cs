using System;

namespace Torque
{
    public class Sampling
    {
        public TimeSpan Time { get; set; }
        public double Frequency { get; set; }
        public TimeSpan Interval => TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond / Frequency));
    }
}
