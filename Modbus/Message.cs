using System;
using System.Linq;
using System.Net;

namespace Modbus
{
    public class Message
    {
        public ushort TransactionId { get; set; } = 0;
        public const ushort PROTOCOL_ID = 0;
        // MBAP长度标头，不是消息字节数
        public ushort Length => (ushort)(2 + Data.Length);
        public byte UnitId { get; set; } = 255;
        public byte FunctionCode { get; set; }
        public byte[] Data { get; set; }

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
            }
            else
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

        public static Message FromBytes(byte[] bytes, int start, int count)
        {
            if (count < 8)
            {
                throw new ProtocolViolationException("modbus消息长度要不少于8字节");
            }
            var transactionId = BigEndianConverter.ToUInt16(new ArraySegment<byte>(bytes, start, 2));
            var protocolId = BigEndianConverter.ToUInt16(new ArraySegment<byte>(bytes, start + 2, 2));
            var length = BigEndianConverter.ToUInt16(new ArraySegment<byte>(bytes, start + 4, 2));
            if (protocolId != PROTOCOL_ID)
            {
                throw new ProtocolViolationException($"modbus消息协议标识要为{PROTOCOL_ID}");
            }
            if (length != count - 6)
            {
                throw new ProtocolViolationException("modbus消息长度标识与收到的字节数不符");
            }
            var data = new byte[count - 8];
            Array.Copy(bytes, start + 8, data, 0, count - 8);
            return new Message
            {
                TransactionId = transactionId,
                UnitId = bytes[start + 6],
                FunctionCode = bytes[start + 7],
                Data = data
            };
        }

        public override string ToString() => string.Join(" ", ToBytes().Select(b => b.ToString("X2")));
    }
}
