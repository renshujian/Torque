using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    public class TorqueService
    {
        public TorqueServiceOptions Options { get; set; }
        public double Threshold { get; set; }
        // 初始容量存储1分钟1000hz数据。
        public List<double> Results { get; } = new(60 * 1000);
        double a;
        double b;
        readonly byte[] buffer = new byte[4];
        Socket? socket;
        CancellationTokenSource cts = new();

        public event Action<double[]>? StopRecording;

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
            a = options.a ?? 15 * 1000 / options.Sensitivity / 248 / 65536;
            b = options.b;
        }

        public Task Zero()
        {
            return Task.CompletedTask;
        }

        public void StartRead()
        {
            if (socket is not null)
            {
                return;
            }
            socket = new(SocketType.Stream, ProtocolType.Tcp);
            var ip = Dns.GetHostAddresses(Options.Host)[0];
            socket.Connect(ip, Options.Port);
            cts = new();
            Task.Run(Read); // TODO: 后台线程错误未传递
        }

        public void StopRead()
        {
            if (cts.IsCancellationRequested) return;
            cts.Cancel();
            cts.Dispose();
        }

        void Read()
        {
            Results.Clear();
            var recording = false;
            var cooldown = 0;
            while (!cts.IsCancellationRequested)
            {
                cooldown--; // 1khz需要25天才溢出下限，可以不做边界检查
                if (socket!.Receive(buffer) == 4 && buffer[2] == 0x0d && buffer[3] == 0x0a)
                {
                    var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(0, 2));
                    var torque = a * value + b;
                    if (torque >= Threshold)
                    {
                        if (!recording && cooldown <= 0)
                        {
                            recording = true;
                        }
                        if (recording)
                        {
                            Results.Add(torque);
                        }
                    }
                    else if (recording)
                    {
                        recording = false;
                        cooldown = 300;
                        StopRecording?.Invoke(Results.ToArray());
                        Results.Clear();
                    }
                }
                else
                {
                    throw new ApplicationException("TorqueService读取的数据帧错误");
                }
            }
            if (socket!.Available >= 4)
            {
                var bytes = new byte[socket.Available];
                socket.Receive(bytes);
                for (int i = 0; i <= bytes.Length - 4; i += 4)
                {
                    cooldown--; // 1khz需要25天才溢出下限，可以不做边界检查
                    var frame = bytes.AsSpan(i, 4);
                    if (frame[2] == 0x0d && frame[3] == 0x0a)
                    {
                        var value = BinaryPrimitives.ReadInt16BigEndian(frame[0..2]);
                        var torque = a * value + b;
                        if (torque >= Threshold)
                        {
                            if (!recording && cooldown <= 0)
                            {
                                recording = true;
                            }
                            if (recording)
                            {
                                Results.Add(torque);
                            }
                        }
                        else if (recording)
                        {
                            recording = false;
                            cooldown = 300;
                            StopRecording?.Invoke(Results.ToArray());
                            Results.Clear();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            socket.Shutdown(SocketShutdown.Both);
            socket.Dispose();
            socket = null;
        }
    }
}
