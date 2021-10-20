namespace Torque
{
    public record TorqueServiceOptions
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 502;
        public double? a { get; init; }
        public double b { get; init; }
        public double Sensitivity { get; init; } = 0.1144;
        public double Threshold { get; init; }
    }
}
