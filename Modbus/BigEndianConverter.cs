using System;
using System.Linq;

namespace Modbus
{
    public static class BigEndianConverter
    {
        public static ushort ToUInt16(ArraySegment<byte> bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
            }
            else
            {
                return BitConverter.ToUInt16(bytes.Array, bytes.Offset);
            }
        }

        public static float ToSingle(ArraySegment<byte> bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToSingle(bytes.Reverse().ToArray(), 0);
            }
            else
            {
                return BitConverter.ToSingle(bytes.Array, bytes.Offset);
            }
        }
    }
}
