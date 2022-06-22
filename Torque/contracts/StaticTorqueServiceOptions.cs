using System;

namespace Torque
{
    public record StaticTorqueServiceOptions
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 502;
        public double Threshold { get; init; } = 0.3;
        public TimeSpan Period { get; init; } = TimeSpan.FromSeconds(4);
        public double? a { get; init; }
        public double b { get; init; }
        public double Sensitivity { get; init; } = 0.1144;
    }
}
