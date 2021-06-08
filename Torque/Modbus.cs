using System;
using System.Net;
using System.Net.Sockets;

namespace Torque
{
    public class Modbus
    {
        public record Message(byte FunctionCode, byte[] Data, ushort TransactionId = 0, byte UnitId = 255)
        {
            public const ushort PROTOCOL_ID = 0;
            // MBAP长度标头，不是消息字节数
            public ushort Length => (ushort)(2 + Data.Length);

            public byte[] ToBytes()
            {
                var result = new byte[8 + Data.Length];
                var b01 = BitConverter.GetBytes(TransactionId);
                var b23 = BitConverter.GetBytes(PROTOCOL_ID);
                var b45 = BitConverter.GetBytes(Length);
                if (BitConverter.IsLittleEndian)
                {
                    result[0] = b01[1];
                    result[1] = b01[0];
                    result[2] = b23[1];
                    result[3] = b23[0];
                    result[4] = b45[1];
                    result[5] = b45[0];
                } else
                {
                    b01.CopyTo(result, 0);
                    b23.CopyTo(result, 2);
                    b45.CopyTo(result, 4);
                }
                result[6] = UnitId;
                result[7] = FunctionCode;
                Data.CopyTo(result, 8);
                return result;
            }

            public static Message FromBytes(byte[] bytes)
            {
                if (bytes.Length < 8)
                {
                    throw new ProtocolViolationException("modbus消息长度要不少于8字节");
                }
                ushort transactionId;
                ushort protocolId;
                ushort length;
                if (BitConverter.IsLittleEndian)
                {
                    transactionId = BitConverter.ToUInt16(new byte[2] { bytes[1], bytes[0] });
                    protocolId = BitConverter.ToUInt16(new byte[2] { bytes[3], bytes[2] });
                    length = BitConverter.ToUInt16(new byte[2] { bytes[5], bytes[4] });
                } else
                {
                    transactionId = BitConverter.ToUInt16(bytes.AsSpan(0..2));
                    protocolId = BitConverter.ToUInt16(bytes.AsSpan(2..4));
                    length = BitConverter.ToUInt16(bytes.AsSpan(4..6));
                }
                if (protocolId != PROTOCOL_ID)
                {
                    throw new ProtocolViolationException($"modbus消息协议标识要为{PROTOCOL_ID}");
                }
                if (length != bytes.Length - 6)
                {
                    throw new ProtocolViolationException("modbus消息长度标识与收到的字节数不符");
                }
                return new Message(bytes[7], bytes[8..], transactionId, bytes[6]);
            }
        }

        public static class FunctionCodes
        {
            public const byte READ_COILS = 1;
            public const byte READ_DISCRETE_INPUTS = 2;
            public const byte READ_HOLDING_REGISTERS = 3;
            public const byte READ_INPUT_REGISTERS = 4;
            public const byte WRITE_SINGLE_COIL = 5;
            public const byte WRITE_SINGLE_REGISTER = 6;
            public const byte WRITE_MULTIPLE_COILS = 15;
            public const byte WRITE_MULTIPLE_REGISTERS = 16;
            public const byte READ_FILE_RECORD = 20;
            public const byte WRITE_FILE_RECORD = 21;
            public const byte MASK_WRITE_REGISTER = 22;
            public const byte READ_WRITE_MULTIPLE_REGISTERS = 23;
            public const byte READ_FIFO_QUEUE = 24;
            public const byte ENCAPSULATED_INTERFACE_TRANSPORT = 43;
        }

        /// <summary>不支持并发请求，工作正确的前提是一个连接上同时只能有一个未完成响应的请求</summary>
        public class Client : IDisposable
        {
            public const int MAX_CONCURRENT_TRANSACTIONS = 1;
            public Socket Socket { get; }
            private byte[] Buffer { get; }

            public Client()
            {
                Socket = new(SocketType.Stream, ProtocolType.Tcp);
                Buffer = new byte[260];
            }

            public void Dispose() => Socket.Dispose();

            public void Connect(IPAddress address, int port = 502) => Socket.Connect(address, port);

            // TODO: timeout, async
            public Message Send(Message request)
            {
                _ = Socket.Send(request.ToBytes());
                var length = ReceiveResponse();
                var response = Message.FromBytes(Buffer[..length]);
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
                    if (bytesLength == 6 && received >=6)
                    {
                        bytesLength = BigEndianConverter.ToUInt16(Buffer.AsSpan(4..6)) + 6;
                    }
                }
                return bytesLength;
            }
        }
    }
}
