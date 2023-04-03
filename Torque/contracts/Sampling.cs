using System;

namespace Torque
{
    public class Sampling
    {
        public TimeSpan Time { get; set; }
        public int Frequency { get; set; }
        public TimeSpan Interval => TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Frequency);
    }
}
