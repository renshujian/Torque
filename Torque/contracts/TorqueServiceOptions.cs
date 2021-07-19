namespace Torque
{
    public record TorqueServiceOptions
    {
        public string Host { get; init; } = "";
        public int Port { get; init; }
    }
}
