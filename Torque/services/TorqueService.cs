using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Modbus;

namespace Torque
{
    class TorqueService : ITorqueService
    {
        public TorqueServiceOptions Options { get; set; }
        public const float a = (10f - -10f) / (20000f - 4000f);
        public float b = -a * 12000;
        // 初始容量存储3分钟60hz数据。实测超过3分钟触发扩容没有可见卡顿
        public List<float> Results { get; } = new(3 * 60 * 60);
        readonly Timer readTimer;
        ModbusClient? modbusClient;
        TaskCompletionSource<float>? readingTCS;

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
            readTimer = new(_ =>
            {
                // List不是线程安全的，但这里可以忽略数据丢失和读取错误。实测14182次读取得到了14154个数据
                ReadSingleAsync().ContinueWith(t => Results.Add(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
            });
        }

        async Task<float> ReadSingleAsync()
        {
            var response = await modbusClient.ReadHoldingRegisters(600, 2);
            var data = new ArraySegment<byte>(response.Data, 1, response.Data.Length - 1);
            var result = BigEndianConverter.ToSingle(new byte[] { data[2], data[3], data[0], data[1] });
            return a * result + b;
        }

        public async Task Zero()
        {
            var ip = Dns.GetHostAddresses(Options.Host)[0];
            modbusClient = new(ip, Options.Port);
            await modbusClient.ConnectAsync();
            var response = await modbusClient.ReadHoldingRegisters(600, 2);
            var data = new ArraySegment<byte>(response.Data, 1, response.Data.Length - 1);
            var result = BigEndianConverter.ToSingle(new byte[] { data[2], data[3], data[0], data[1] });
            b = -a * result;
            modbusClient?.Dispose();
            modbusClient = null;
        }

        public async Task<float> ReadAsync()
        {
            Results.Clear();
            var ip = Dns.GetHostAddresses(Options.Host)[0];
            modbusClient = new(ip, Options.Port);
            await modbusClient.ConnectAsync();
            readTimer.Change(0, 1000 / Options.HZ);
            readingTCS = new();
            return await readingTCS.Task;
        }

        public void StopRead()
        {
            readTimer.Change(Timeout.Infinite, Timeout.Infinite);
            modbusClient?.Dispose();
            modbusClient = null;
            File.WriteAllText($"torque-{DateTime.Now:yyyyMMddHHmmss}.txt", string.Join("\r\n", Results));
            Results.Sort((x, y) => y.CompareTo(x));
            var result = Results.Take(Options.Sample).DefaultIfEmpty().Average();
            readingTCS?.SetResult(result);
            readingTCS = null;
        }
    }
}
