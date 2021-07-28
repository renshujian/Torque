using System;

namespace Modbus
{
    public static class ArraySegmentExtensions
    {
        public static ArraySegment<T> Slice<T>(this ArraySegment<T> self, int start, int count)
        {
            return new ArraySegment<T>(self.Array, self.Offset + start, count);
        }

        public static ArraySegment<T> Slice<T>(this ArraySegment<T> self, int start) => Slice(self, start, self.Count - start);

        public static T[] ToArray<T>(this ArraySegment<T> self)
        {
            var result = new T[self.Count];
            Array.Copy(self.Array, self.Offset, result, 0, result.Length);
            return result;
        }
    }
}
