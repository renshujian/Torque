using System;
using System.Diagnostics;

namespace Modbus
{
    internal class ArrayBuffer
    {
        private byte[] bytes;
        private int activeStart;
        private int availableStart;

        public ArrayBuffer(int size)
        {
            bytes = new byte[size];
        }

        public int ActiveLength => availableStart - activeStart;
        public ArraySegment<byte> ActiveSegment => new ArraySegment<byte>(bytes, activeStart, ActiveLength);
        public int AvailableLength => bytes.Length - availableStart;
        public ArraySegment<byte> AvailableSegment => new ArraySegment<byte>(bytes, availableStart, AvailableLength);

        public void Commit(int count)
        {
            Debug.Assert(count <= AvailableLength);
            availableStart += count;
        }

        public void Discard(int count)
        {
            Debug.Assert(count <= ActiveLength);
            activeStart += count;
            if (activeStart == availableStart)
            {
                activeStart = availableStart = 0;
            }
        }
    }
}
