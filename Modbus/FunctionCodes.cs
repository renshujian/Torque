namespace Modbus
{
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
}
