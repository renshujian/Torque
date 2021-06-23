using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Modbus
{
    public class ModbusClient : IDisposable
    {
        public IPAddress IP { get; }
        public int Port { get; }
        private Socket Socket { get; set; }
        private ushort transactionId;
        private readonly ArrayBuffer buffer;
        private readonly CancellationTokenSource cts;
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Message>> pendingTasks;

        public ModbusClient(IPAddress ip, int port = 502)
        {
            IP = ip;
            Port = port;
            buffer = new ArrayBuffer(4096);
            cts = new CancellationTokenSource();
            pendingTasks = new ConcurrentDictionary<ushort, TaskCompletionSource<Message>>();
            _ = DoReceiveAsync(cts.Token); // TODO: 如何处理receiveTask?
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Dispose();
        }

        public Task<Message> ReadHoldingRegisters(ushort address, ushort count)
        {
            var b12 = BitConverter.GetBytes(address);
            var b34 = BitConverter.GetBytes(count);
            if (BitConverter.IsLittleEndian)
            {
                var data = new byte[] { b12[1], b12[0], b34[1], b34[0] };
                return RequestAsync(FunctionCodes.READ_HOLDING_REGISTERS, data);
            }
            else
            {
                return RequestAsync(FunctionCodes.READ_HOLDING_REGISTERS, b12.Concat(b34).ToArray());
            }
        }

        public async Task<Message> RequestAsync(byte functionCode, byte[] data)
        {
            var request = new Message()
            {
                TransactionId = transactionId++, // TODO: 高并发时是否会出错？
                FunctionCode = functionCode,
                Data = data
            };
            var response = await SendAsync(request);
            if (response.TransactionId != request.TransactionId)
            {
                throw new ProtocolViolationException($"modbus响应的事务标识{response.TransactionId}与请求{request.TransactionId}不符，是否对同一个连接进行了并发请求？");
            }
            if (response.FunctionCode == request.FunctionCode + 128)
            {
                throw new ModbusException($"modbus服务器返回了异常码{response.Data[0]}");
            }
            if (response.FunctionCode != request.FunctionCode)
            {
                throw new ProtocolViolationException($"modbus响应的功能码{response.FunctionCode}与请求{request.FunctionCode}不符");
            }
            return response;
        }

        private Task<Message> SendAsync(Message message)
        {
            var sendTask = Socket.SendAsync(new ArraySegment<byte>(message.ToBytes()), SocketFlags.None);
            // TODO: 没有处理sendTask.Result
            sendTask.ContinueWith(t =>
            {
                if (pendingTasks.TryRemove(message.TransactionId, out var cs))
                {
                    cs.SetException(t.Exception);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
            var tcs = new TaskCompletionSource<Message>();
            pendingTasks[message.TransactionId] = tcs;
            return tcs.Task;
        }

        private async Task DoReceiveAsync(CancellationToken cancellation)
        {
            // Socket.Disconnect(true)后马上调用Socket.ConnectAsync会导致ModRSsim2崩溃，所以new Socket
            Task connectAsync()
            {
                Socket?.Shutdown(SocketShutdown.Both);
                Socket?.Dispose();
                Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                return Socket.ConnectAsync(IP, Port);
            }

            while (true)
            {
                if (Socket?.Connected != true)
                {
                    await connectAsync();
                }
                var received = await Socket.ReceiveAsync(buffer.AvailableSegment, SocketFlags.None);
                // 连接中断时ReceiveAsync同步返回0，Socket.Connected仍为true，不加下面这个判断的话会无限循环阻塞线程
                // Socket没有设置超时，连接正常时应该会一直await到Available才返回，received不会等于0
                if (received == 0)
                {
                    await connectAsync();
                }
                buffer.Commit(received);
                if (Message.TryParseBytesLength(buffer.ActiveSegment, out int length))
                {
                    var message = Message.FromBytes(buffer.ActiveSegment.Slice(0, length));
                    buffer.Discard(length);
                    if (pendingTasks.TryRemove(message.TransactionId, out var tcs))
                    {
                        tcs.SetResult(message);
                    }
                }
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}

