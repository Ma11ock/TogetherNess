using System;

namespace TogetherNess.Hardware;

public class InvalidInstructionStepException : Exception
{
    public readonly byte OpCode;
    
    public InvalidInstructionStepException(byte opCode) : base($"Invalid instruction step for opcode {opCode}")
    {
        OpCode = opCode;
    }
}