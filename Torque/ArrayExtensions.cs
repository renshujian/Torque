using System;
using System.Text;

namespace Torque
{
    public static class ArrayExtensions
    {
        public static string ToMemberString<T>(this T[] array)
        {
            StringBuilder builder = new();
            builder.Append('[');
            foreach (var i in array)
            {
                builder.Append(i).Append(',');
            }
            return builder.Append(']').ToString();
        }

        public static string ToMemberString(this byte[] array)
        {
            StringBuilder builder = new();
            builder.Append('[');
            foreach (var i in array)
            {
                builder.AppendFormat("{0:X2}", i).Append(',');
            }
            return builder.Append(']').ToString();
        }
    }
}
