using System;

namespace Torque
{
    public class Test
    {
        public string ToolId { get; set; } = "";
        public double SetTorque { get; set; }
        public double RealTorque { get; set; }
        public double Diviation { get; set; }
        public DateTime TestTime { get; set; }
    }
}
