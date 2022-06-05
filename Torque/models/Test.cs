using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Torque
{
    public class Test
    {
        public string ToolId { get; init; } = "";
        public double SetTorque { get; init; }
        public double RealTorque { get; init; }
        public double Diviation { get; init; }
        public DateTime TestTime { get; init; }
        [NotMapped] public double AllowedDiviation { get; init; } = 0.2;
        public bool IsOK => Math.Abs(Diviation) <= AllowedDiviation;
        public string IsPass => IsOK ? "PASS" : "NG";
    }
}
