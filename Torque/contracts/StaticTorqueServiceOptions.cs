using System;

namespace Torque
{
    public record StaticTorqueServiceOptions
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 502;
        public double BeginThreshold { get; init; } = 0.3;
        public TimeSpan EndTimeSpan { get; init; } = TimeSpan.Zero;
        public double EndThreshold { get; init; } = 0.2;
        public int BeginSkip { get; init; }
        public int EndSkip { get; init; }
        public double? a { get; init; }
        public double b { get; init; }
        public double Sensitivity { get; init; } = 0.1144;
    }
}
