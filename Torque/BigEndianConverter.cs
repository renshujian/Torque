using System;
using System.Linq;

namespace Torque
{
    public static class BigEndianConverter
    {
        public static ushort ToUInt16(ReadOnlySpan<byte> bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToUInt16(new byte[2] { bytes[1], bytes[0] });
            } else
            {
                return BitConverter.ToUInt16(bytes);
            }
        }

        public static float ToSingle(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToSingle(bytes.Reverse().ToArray());
            } else
            {
                return BitConverter.ToSingle(bytes);
            }
        }
    }
}
