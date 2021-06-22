using System;
using System.Net;
using System.Net.Sockets;

namespace Modbus
{
    /// <summary>不支持并发请求，工作正确的前提是一个连接上同时只能有一个未完成响应的请求</summary>
    public class ModbusClient : IDisposable
    {
        public const int MAX_CONCURRENT_TRANSACTIONS = 1;
        public Socket Socket { get; }
        private byte[] Buffer { get; }

        public ModbusClient()
        {
            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Buffer = new byte[260];
        }

        public void Dispose() => Socket.Dispose();

        public void Connect(IPAddress address, int port = 502) => Socket.Connect(address, port);

        // TODO: timeout, async
        public Message Send(Message request)
        {
            _ = Socket.Send(request.ToBytes());
            var length = ReceiveResponse();
            var response = Message.FromBytes(Buffer, 0, length);
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

        private int ReceiveResponse()
        {
            int received = 0;
            int bytesLength = 6;
            while (received < bytesLength)
            {
                received += Socket.Receive(Buffer, received, bytesLength, SocketFlags.None);
                if (bytesLength == 6 && received >= 6)
                {
                    bytesLength = BigEndianConverter.ToUInt16(new ArraySegment<byte>(Buffer, 4, 2)) + 6;
                }
            }
            return bytesLength;
        }
    }
}

