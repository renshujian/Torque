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
        public Task ReceiveTask { get; private set; } // socket连接出错时IsFaulted，cancel时IsCompleted
        private Socket socket;
        private readonly CancellationTokenSource receiveCts;
        private ushort transactionId;
        private readonly ArrayBuffer buffer;
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Message>> pendingTasks;

        public ModbusClient(IPAddress ip, int port = 502)
        {
            IP = ip;
            Port = port;
            receiveCts = new CancellationTokenSource();
            buffer = new ArrayBuffer(4096);
            pendingTasks = new ConcurrentDictionary<ushort, TaskCompletionSource<Message>>();
        }

        public async Task ConnectAsync()
        {
            if (socket != null)
            {
                throw new ModbusException("ModbusClient.ConnectAsync只能调用一次，请创建新的实例");
            }
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(IP, Port);
            ReceiveTask = DoReceiveAsync(receiveCts.Token);
        }

        public void Dispose()
        {
            receiveCts.Cancel();
            receiveCts.Dispose();
            socket.Dispose();
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
                throw new ModbusException($"modbus响应的事务标识{response.TransactionId}与请求{request.TransactionId}不符，是否对同一个连接进行了并发请求？");
            }
            if (response.FunctionCode == request.FunctionCode + 128)
            {
                throw new ModbusException($"modbus服务器返回了异常码{response.Data[0]}");
            }
            if (response.FunctionCode != request.FunctionCode)
            {
                throw new ModbusException($"modbus响应的功能码{response.FunctionCode}与请求{request.FunctionCode}不符");
            }
            return response;
        }

        private async Task<Message> SendAsync(Message message)
        {
            var packet = message.ToBytes();
            var total = packet.Length;
            var sent = 0;
            while (sent < total)
            {
                sent += await socket.SendAsync(new ArraySegment<byte>(packet, sent, total - sent), SocketFlags.None);
            }
            var tcs = new TaskCompletionSource<Message>();
            pendingTasks[message.TransactionId] = tcs;
            return await tcs.Task;
        }

        private async Task DoReceiveAsync(CancellationToken cancellation)
        {
            while (true)
            {
                if (socket?.Connected != true)
                {
                    await ReConnectAsync();
                }
                var received = await socket.ReceiveAsync(buffer.AvailableSegment, SocketFlags.None);
                // 连接中断时ReceiveAsync同步返回0，Socket.Connected仍为true，不加下面这个判断的话会无限循环阻塞线程
                // Socket没有设置超时，连接正常时应该会一直await到Available才返回，received不会等于0
                if (received == 0)
                {
                    await ReConnectAsync();
                }
                else
                {
                    buffer.Commit(received);
                    int length;
                    while (Message.TryParseBytesLength(buffer.ActiveSegment, out length))
                    {
                        var message = Message.FromBytes(buffer.ActiveSegment.Slice(0, length));
                        buffer.Discard(length);
                        TaskCompletionSource<Message> tcs;
                        if (pendingTasks.TryRemove(message.TransactionId, out tcs))
                        {
                            tcs.SetResult(message);
                        }
                    }
                }
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        // Socket.Disconnect(true)后马上调用Socket.ConnectAsync会导致ModRSsim2崩溃，所以new Socket
        private Task ReConnectAsync()
        {
            socket?.Shutdown(SocketShutdown.Both);
            socket?.Dispose();
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            return socket.ConnectAsync(IP, Port);
        }
    }
}

