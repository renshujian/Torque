using System;
using System.Collections.Generic;

namespace Torque
{
    public class StaticTorqueServiceOptions
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 502;
        public int SaveCount { get; init; } = 12;

        private List<StaticTorqueParameter> _parameters = new();
        public IReadOnlyList<StaticTorqueParameter> Parameters => _parameters;

        /// <summary>
        /// 按照目标阈值从低到高的顺序插入参数集列表。相同目标阈值时后加入的项排在前面，使之前加入的项不会被GetParameter获取到
        /// </summary>
        /// <param name="parameter"></param>
        public void AddParameter(StaticTorqueParameter parameter)
        {
            var index = _parameters.FindIndex(it => it.TargetUnder >= parameter.TargetUnder);
            if (index == -1)
            {
                // 没有找到大于等于当前目标阈值的参数集，当前参数集加入列表末尾
                _parameters.Add(parameter);
            }
            else
            {
                // 找到大于等于当前目标阈值的参数集，当前参数集插入该位置
                _parameters.Insert(index, parameter);
            }
        }

        public StaticTorqueParameter GetParameter(double targetValue) => _parameters.Find(it => it.TargetUnder >= targetValue) ?? throw new ArgumentException("没有适合目标的参数集");
    }

    public record StaticTorqueParameter
    {
        public double TargetUnder { get; init; }
        public double BeginThreshold { get; init; } = 0.3;
        public TimeSpan EndTimeSpan { get; init; } = TimeSpan.Zero;
        public double EndThreshold { get; init; } = 0.2;
        public int BeginSkip { get; init; }
        public int EndSkip { get; init; }
        public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(0.3);
        public double? a { get; init; }
        public double? b { get; init; }
        public double Sensitivity { get; init; } = 0.1144;
    }
}
