using System;

namespace Torque
{
    public class ModbusException : Exception
    {
        public ModbusException(string? message) : base(message)
        {
        }

        public ModbusException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
