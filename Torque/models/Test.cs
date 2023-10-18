using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Torque
{
    public class Test
    {
        public string ToolId { get; init; } = "";
        public double SetTorque { get; init; }
        public double RealTorque { get; set; }
        public double Diviation { get; set; }
        public DateTime TestTime { get; init; }
        [NotMapped] public double AllowedDiviation { get; init; } = 0.2;
        [NotMapped] public string ResultPath { get; init; } = string.Empty;
        [NotMapped] public TorqueServiceOptions? TorqueServiceOptions { get; init; }
        [NotMapped] public Sampling[] Samplings { get; init; } = Array.Empty<Sampling>();
        public bool IsOK => Math.Abs(Diviation) <= AllowedDiviation;
        public string IsPass => IsOK ? "PASS" : "NG";
    }
}
