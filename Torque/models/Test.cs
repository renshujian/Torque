using System;

namespace Torque
{
    public class Test
    {
        public string ToolId { get; init; } = "";
        public double SetTorque { get; init; }
        public double RealTorque { get; init; }
        public double Diviation { get; init; }
        public DateTime TestTime { get; init; }
        public bool IsOK => Diviation is >= -0.2 and <= 0.2;
        public string IsPass => IsOK ? "PASS" : "NG";
    }
}
