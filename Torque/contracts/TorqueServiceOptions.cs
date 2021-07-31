namespace Torque
{
    public record TorqueServiceOptions
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 502;
        public int Sample { get; init; } = 10;
    }
}
