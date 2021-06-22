using System;
using System.Collections.Generic;
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

        public static Message FromBytes(ArraySegment<byte> bytes)
        {
            if (bytes.Count < 8)
            {
                throw new ProtocolViolationException("modbus消息长度要不少于8字节");
            }
            var transactionId = BigEndianConverter.ToUInt16(bytes.Slice(0, 2));
            var protocolId = BigEndianConverter.ToUInt16(bytes.Slice(2, 2));
            var length = BigEndianConverter.ToUInt16(bytes.Slice(4, 2));
            if (protocolId != PROTOCOL_ID)
            {
                throw new ProtocolViolationException($"modbus消息协议标识要为{PROTOCOL_ID}");
            }
            if (length != bytes.Count - 6)
            {
                throw new ProtocolViolationException("modbus消息长度标识与收到的字节数不符");
            }
            var list = (IList<byte>)bytes;
            return new Message
            {
                TransactionId = transactionId,
                UnitId = list[6],
                FunctionCode = list[7],
                Data = bytes.Slice(8).ToArray()
            };
        }

        public static bool TryParseBytesLength(ArraySegment<byte> bytes, out int length)
        {
            if (bytes.Count < 6)
            {
                length = bytes.Count;
                return false;
            }
            length = BigEndianConverter.ToUInt16(bytes.Slice(4, 2)) + 6;
            return true;
        }

        public override string ToString() => string.Join(" ", ToBytes().Select(b => b.ToString("X2")));
    }
}
