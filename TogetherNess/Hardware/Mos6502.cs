using System.Diagnostics;
using TogetherNess.Utils;

namespace TogetherNess.Hardware;

// Questions: Does the CPU pipeline, i.e. does it read the next instruction if the memory bus is being otherwise unused.

public class Mos6502(in IMemory memory)
{
    private enum DestinationRegister
    {
        IndexX,
        IndexY,
        Accumulator,
        StackPointer,
    }
    
    private const Timing FIRST_CYCLE = Timing.T2;
    
    private const Timing TWO_CYCLE_FIRST_TIMING = Timing.T0 | Timing.T2;
    
    private const Timing LAST_CYCLE_TIMING = Timing.TPlus | Timing.T1;
    
    /// <summary>
    /// Memory system.
    /// </summary>
    private readonly IMemory _memory = memory;
    
    /// <summary>
    /// Is true if the CPU is executing an instruction, false if the CPU is in between instructions.
    /// </summary>
    public bool IsInMiddleOfInstruction => (InstructionTimer & FIRST_CYCLE) != FIRST_CYCLE;

    /// <summary>
    /// Destination of the current memory read operation.
    /// </summary>
    public MemoryDataDestination MemoryDataDestination { get; set; } = MemoryDataDestination.InstructionRegister;
    
    /// <summary>
    /// The current clock cycle in the instruction.
    /// </summary>
    public Timing InstructionTimer { get; set; } = Timing.T2 | Timing.TPlus;
    
    /// <summary>
    /// Temporary latch used to calculate memory addresses.
    /// </summary>
    public ushort DataPointer { get; set; } = 0;

    public byte DataPointerLow
    {
        get => (byte)DataPointer;
        set => DataPointer = (ushort)((DataPointer & 0xFF00) | value);
    }
    
    public byte DataPointerHigh
    {
        get => (byte)(DataPointer >> 8);
        set => DataPointer = (ushort)((DataPointer & 0x00FF) | (value << 8));
    }
    
    /// <summary>
    /// Effective Address. Contains the memory address that the current cycle's memory operation
    /// will be done on.
    /// </summary>
    public ushort MemoryAddressRegister { get; set; } = 0;
    
    public byte MemoryAddressLow
    {
        get => (byte)MemoryAddressRegister;
        set => MemoryAddressRegister = (ushort)((MemoryAddressRegister & 0xFF00) | value);
    }
    
    public byte MemoryAddressHigh
    {
        get => (byte)(MemoryAddressRegister >> 8);
        set => MemoryAddressRegister = (ushort)((MemoryAddressRegister & 0x00FF) | (value << 8));
    }
    
    /// <summary>
    /// Program Counter.
    /// </summary>
    public ushort ProgramCounter { get; set; } = 0;
    
    /// <summary>
    /// Accumulator.
    /// </summary>
    public byte Accumulator { get; set; } = 0xAA;

    /// <summary>
    /// X index register.
    /// </summary>
    public byte X { get; set; } = 0;

    /// <summary>
    /// Y index register.
    /// </summary>
    public byte Y { get; set; } = 0;

    /// <summary>
    /// Stack pointer.
    /// </summary>
    public byte StackPointer { get; set; } = 0;
    
    /// <summary>
    /// Status register (only 6 bits are used by the ALU).
    /// </summary>
    public byte Status { get; set; } = 0;

    /// <summary>
    /// Input data latch from memory.
    /// </summary>
    public byte MemoryDataRegister { get; set; } = 0;
    
    /// <summary>
    /// Output register for memory.
    /// </summary>
    public byte MemoryDataOutputRegister { get; set; } = 0;
    
    /// <summary>
    /// Register that contains the current executing instruction.
    /// </summary>
    public byte InstructionRegister { get; set; } = 0x00;
    
    /// <summary>
    /// Register used for internal operations and for storing results for later (e.g. the result of RMW instructions).
    /// </summary>
    public byte TempRegister { get; set; } = 0x00;

    /// <summary>
    /// If true signals a memory read. If false then a memory write.
    /// </summary>
    public bool ReadPin { get; set; } = false;

    /// <summary>
    /// True if the last ALU operation resulted in a carry bit, false if not.
    /// <see cref="CarryOut"/> is different from <see cref="CarryBit"/> because <see cref="CarryBit"/> is part of the
    /// status register and can be used by the programmer. <see cref="CarryOut"/> is used only for internal CPU
    /// operations.
    /// </summary>
    public bool CarryOut { get; set; } = false;

    public bool NegativeBit
    {
        get => Status.GetBit(0) != 0;
        set => Status.SetBitValue(0, value);
    }

    public bool ZeroBit
    {
        get => Status.GetBit(1) != 0;
        set => Status.SetBitValue(1, value);
    }

    public bool CarryBit
    {
        get => Status.GetBit(2) != 0;
        set => Status.SetBitValue(2, value);
    }

    public bool InterruptBit
    {
        get => Status.GetBit(3) != 0;
        set => Status.SetBitValue(3, value);
    }

    public bool DecimalBit
    {
        get => Status.GetBit(4) != 0;
        set => Status.SetBitValue(4, value);
    }

    public bool OverflowBit
    {
        get => Status.GetBit(5) != 0;
        set => Status.SetBitValue(5, value);
    }

    public void Cycle()
    {
        // Address latch is PC by default. This is essentially φ1 of the 6502's cycle.
        MemoryAddressRegister = ProgramCounter;
        ReadPin = true;
        
        // φ2 of the 6502's cycle.
        // Perform memory operation and perform next step of the current instruction.

        {
            // We only want the bottom 5 bits, excluding T0. No SD1, T6, T1, etc..
            int regularTiming = ((int)InstructionTimer >> 1) & 0x1F;
            // Assert that only one of TPlus-T5 is HIGH.
            Debug.Assert((regularTiming & (regularTiming - 1)) == 0);
        }
        // MOS 6502 opcodes are made up of three parts: aaabbbcc
        // aaa: Type (e.g. sbc, adc, ror, etc.)
        // bbb: Address mode (e.g. implied, immediate, absolute X, etc).
        // cc: Clarifying type.
        // See: https://llx.com/Neil/a2/opcodes.html
        int bbb = (InstructionRegister >> 2) & 0x7; // Instruction address mode.
        int cc = InstructionRegister & 0x03; // Instruction subtype.
        bool twoCycleInstruction = (bbb == 0x2 && cc == 0x1) ||
                                   (bbb == 0x0 && cc is 0x3 or 0x0) ||
                                   InstructionRegister == 0x18 ||
                                   InstructionRegister == 0x38 ||
                                   InstructionRegister == 0x58 ||
                                   InstructionRegister == 0x78 ||
                                   InstructionRegister == 0xB8 ||
                                   InstructionRegister == 0xD8 ||
                                   InstructionRegister == 0xF8 ||
                                   InstructionRegister == 0xEA;
        
        bool instructionFetch = (InstructionTimer | Timing.T1) == Timing.T1;
        Timing nextTiming = InstructionDecodeAndRun(InstructionTimer);
        
        if (instructionFetch)
        {
            // Address latch is the next opcode or instruction operand.
            MemoryDataDestination = MemoryDataDestination.InstructionRegister;
            SetupReadFor(ProgramCounter);
        }
        
        // No current instruction. Decode the next instruction.
        if (ReadPin)
        {
            // Data read.
            MemoryDataRegister = (byte)_memory[MemoryAddressRegister];
        }
        else
        {
            // Data write.
            _memory[MemoryAddressRegister] = MemoryDataRegister;
        }

        switch (MemoryDataDestination)
        {
            case MemoryDataDestination.InstructionRegister:
                InstructionRegister = MemoryDataRegister;
                // Predecode: Determine if the instruction is 1 byte.
                // This is done for accuracy with interrupts. Interrupts only trigger if the T0 on an instruction
                // was set, but branches not taken do not set the T0 until the next instruction, causing a delay.
                // We need to emulate T0 behavior to achieve that delay.
                bool oneByteInstruction = (InstructionRegister & 0x08) == 0x08 ||
                                          (InstructionRegister & 0x0A) == 0x0A ||
                                          InstructionRegister == 0x40 ||
                                          InstructionRegister == 0x60;

            
                InstructionTimer |= nextTiming == Timing.T2 && oneByteInstruction ? Timing.T0 : 0;
                break;
            case MemoryDataDestination.DataPointerLow:
                DataPointerLow = MemoryDataRegister;
                break;
            case MemoryDataDestination.DataPointerHigh:
                DataPointerHigh = MemoryDataRegister;
                break;
        }

        ProgramCounter += (ushort)(!twoCycleInstruction
                                   || (InstructionTimer | Timing.T1) == Timing.T1  ? 1 : 0);
        
        InstructionTimer = nextTiming;
    }
    
    /// <summary>
    /// Perform an ALU addition operation on a, b, and the carry bit.
    /// </summary>
    /// <param name="a">The operator for addition. ALU's a register.</param>
    /// <param name="b">The other operator for addition. ALU's b register.</param>
    /// <param name="carry">The carry bit.</param>
    /// <returns>(In order) The addition's result, the negative bit, the zero bit, the carry bit, the overflow bit.</returns>
    public (byte, bool, bool, bool, bool) AluAdd(int a, int b, bool carry)
    {
        int result = a + b + (carry ? 1 : 0);
        return ((byte)result,
            (result & 0x80) != 0,
            result == 0,
            result > 0xFF,
            ((result ^ a) & (result ^ b) & 0x80) != 0);
    }

    /// <summary>
    /// Perform a logical right shift on a and return the result and the status register states.
    /// </summary>
    /// <param name="a">The value to logical shift right.</param>
    /// <returns>(In order) The result of the shift, the negative bit, the zero bit, and the carry bit.</returns>
    public (byte, bool, bool, bool) AluLogicalShiftRight(int a)
    {
        uint result = (uint)a >> 1;
        return ((byte)result, false, result == 0, (result & 1) == 1);
    }
    
    public (byte, bool, bool) AluXor(int a, int b)
    {
        int result = a ^ b;
        return ((byte)result,
            (result & 0x80) == 0x80,
            result == 0);
    }
    
    public (byte, bool, bool) AluOr(int a, int b)
    {
        int result = a | b;
        return ((byte)result, 
            result == 0,
            (result & 0x80) != 0);
    }
    
    public (byte, bool, bool) AluAnd(int a, int b)
    {
        int result = a & b;
        return ((byte)result, 
            result == 0,
            (result & 0x80) != 0);
    }

    public (byte, bool, bool, bool) AluAsl(int a)
    {
        int result = a << 1;
        return ((byte)result, (a & 0x80) != 0, result == 0, (result & 0x80) != 0);
    }

    public (byte, bool, bool, bool) AluRotateLeft(int a)
    {
        int result = (byte)(a << 1) | (byte)(a >> (8 - 1));
        return ((byte)result, (result & 0x80) == 0x80, result == 0, (a & 0x80) == 0x80);
    }

    public (bool, bool, bool) AluCompare(int a, int b)
    {
        var (_, negative, zero, carry, __) = AluAdd(a, -b, false);
        return (negative, zero, carry);
    }
    
    private void SetupReadFor(ushort address)
    {
        MemoryAddressRegister = address;
        ReadPin = true;
    }

    /// <summary>
    /// Perform an ALU addition operation on b and the <see cref="Accumulator"/> and store the result in
    /// the <see cref="Accumulator"/> and the status registers.
    /// Micro operations:
    /// A ← A + b + C
    /// N ← A gt 0
    /// Z ← A = 0
    /// C ← 1 if there was unsigned integer overflow, otherwise 0
    /// V ← 1 if there was signed integer overflow, otherwise 0
    /// </summary>
    /// <param name="b">The other operator for addition. ALU's b register.</param>
    /// <returns><see cref="Timing.T2"/>, as this is always the last operation in an instruction.</returns>
    private Timing LogicAdd(int b)
    {
        var (result, negative, zero, carry, overflow) = AluAdd(Accumulator, b, CarryBit);
        Accumulator = result;
        CarryBit = carry;
        OverflowBit = overflow;
        ZeroBit = zero;
        NegativeBit = negative;
        return Timing.T2;
    }

    /// <summary>
    /// Perform an ALU addition operation on the <see cref="Accumulator"/> and the 2's complement of b
    /// and store the result in the <see cref="Accumulator"/> and the status registers.
    /// Micro operations:
    /// A ← A - b + C
    /// N ← A gt 0
    /// Z ← A = 0
    /// C ← 1 if there was unsigned integer overflow, otherwise 0
    /// V ← 1 if there was signed integer overflow, otherwise 0
    /// </summary>
    /// <param name="b">The other operator for addition. ALU's b register.</param>
    /// <returns><see cref="Timing.T2"/>, as this is always the last operation in an instruction.</returns>
    private Timing LogicSubtract(int b) => LogicAdd(-b);

    private void LogicCompare(int a, int b)
    {
        var (negative, zero, carry) = AluCompare(a, b);
        CarryBit = carry;
        ZeroBit = zero;
        NegativeBit = negative;
    }

    private Timing LogicAnd(int a)
    {
        var (result, negative, zero) = AluAnd(Accumulator, a);
        Accumulator = result;
        NegativeBit = negative;
        ZeroBit = zero;
        return Timing.T2;
    }
    
    private Timing LogicOr(int a)
    {
        var (result, zero, negative) = AluOr(Accumulator, a);
        Accumulator = result;
        ZeroBit = zero;
        NegativeBit = negative;
        return Timing.T2;
    }

    private Timing LogicXor(int a)
    {
        var (result, negative, zero) = AluXor(Accumulator, a);
        Accumulator = result;
        ZeroBit = zero;
        NegativeBit = negative;
        return Timing.T2;
    }
    
    private Timing InstructionDecodeAndRun(Timing instructionTimer) 
        => InstructionRegister switch 
        {
            // BRK.
            0x0 => instructionTimer switch
            {
               Timing.T2 => BrkCycle1(),
               Timing.T3 => BrkCycle2(),
               Timing.T4 => BrkCycle3(),
               Timing.T5 | Timing.V0 => BrkCycle4(),
               Timing.T6 =>BrkCycle5(),
               Timing.T0 => BrkCycle6(),
               Timing.TPlus | Timing.T1 => BrkCycle7(),
                _ => throw new InvalidInstructionStepException(0x0)
            },
            // ORA indirect, X.
            0x1 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectXCycle2(),
               Timing.T4 => IndirectXCycle3(),
               Timing.T5 => IndirectXCycle4(),
               Timing.T0 => IndirectXCycle5(false),
               Timing.TPlus => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x1)
            },
            // Jam (illegal)
            0x2 => Jam(),
            // SLO indirect, X (illegal)
            0x3 => instructionTimer switch
            {
               Timing.T2 => SloIndirectXCycle1(),
               Timing.T3 =>SloIndirectXCycle2(),
               Timing.T4 => SloIndirectXCycle3(),
               Timing.T5 => SloIndirectXCycle4(),
               Timing.T6 =>SloIndirectXCycle5(),
               Timing.T0 => SloIndirectXCycle6(),
               Timing.TPlus => SloIndirectXCycle7(),
               Timing.T8 => SloIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0x3)
            },
            // NOP zeropage (illegal)
            0x4 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x4)
            },
            // ORA zeropage
            0x5 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x5)
            },
            // ASL zeropage
            0x6 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 | Timing.SD1 => ZeropageRead(),
               Timing.T4 | Timing.SD2 => Asl(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x6)
            },
            // SLO zeropage
            0x7 => instructionTimer switch
            {
               Timing.T2 => SloZeropageCycle1(),
               Timing.T3 =>SloZeropageCycle2(),
               Timing.T4 => SloZeropageCycle3(),
               Timing.T5 => SloZeropageCycle4(),
               Timing.T6 =>SloZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0x7)
            },
            // PHP zeropage
            0x8 => instructionTimer switch
            {
               Timing.T2 => PhpCycle1(),
               Timing.T3 =>PhpCycle2(),
               Timing.T4 => PhpCycle3(),
                _ => throw new InvalidInstructionStepException(0x8)
            },
            // ORA imm
            0x9 => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x9)
            },
            // ASL accum
            0xA => instructionTimer switch
            {
               Timing.T2 => AslAccumCycle1(),
               Timing.T3 =>AslAccumCycle2(),
                _ => throw new InvalidInstructionStepException(0xA)
            },
            // ANC imm (illegal)
            0xB => instructionTimer switch
            {
               Timing.T2 => AncCycle1(),
               Timing.T3 =>AncCycle2(),
               Timing.T4 => AncCycle3(),
                _ => throw new InvalidInstructionStepException(0xB)
            },
            // Nop abs (illegal)
            0xC => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(Timing.T0),
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T0),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xC)
            },
            // Ora abs
            0xD => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(Timing.T0),
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T1),
               Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0xD)
            },
            // ASL abs
            0xE => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => Asl(),
               Timing.T0 => Write(DataPointer, TempRegister),
               Timing.TPlus | Timing.T1 => FIRST_CYCLE,
                _ => throw new InvalidInstructionStepException(0xE)
            },
            // SLO abs
            0xF => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => Slo(),
               Timing.T6 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => FIRST_CYCLE,
                _ => throw new InvalidInstructionStepException(0xF)
            },
            // BPL
            0x10 => instructionTimer switch
            {
               Timing.T2 => BplCycle1(),
               Timing.T3 => BplCycle2(),
               Timing.T4 => BplCycle3(),
               Timing.T5 => BplCycle4(),
                _ => throw new InvalidInstructionStepException(0x10)
            },
            // ORA indirect, Y
            0x11 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectYCycle2(),
               Timing.T4 => IndirectYCycle3(),
               Timing.T5 => IndirectYCycle4(),
               Timing.T0 => IndirectYCycle5(),
               Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x11)
            },
            // JAM (illegal)
            0x12 => Jam(),
            // 0x13 SLO indirect, Y
            0x13 => instructionTimer switch
            {
               Timing.T2 => SloIndirectYCycle1(),
               Timing.T3 =>SloIndirectYCycle2(),
               Timing.T4 => SloIndirectYCycle3(),
               Timing.T5 => SloIndirectYCycle4(),
               Timing.T6 =>SloIndirectYCycle5(),
               Timing.T0 => SloIndirectYCycle6(),
               Timing.TPlus => SloIndirectYCycle7(),
               Timing.T8 => SloIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0x13)
            },       
            // 0x14 NOP zeropage, X (illegal)
            0x14 => instructionTimer switch
            {
               Timing.T2 => NopZeropageCycle1(),
               Timing.T3 =>NopZeropageCycle2(),
               Timing.T4 => NopZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x14)
            },       
            // 0x15 ORA zeropage, X
            0x15 => instructionTimer switch
            {
               Timing.T2 => OraZeropageXCycle1(),
               Timing.T3 =>OraZeropageXCycle2(),
               Timing.T4 => OraZeropageXCycle3(),
               Timing.T5 => OraZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x15)
            },       
            // 0x16 ASL zeropage, X
            0x16 => instructionTimer switch
            {
               Timing.T2 => AslZeropageXCycle1(),
               Timing.T3 =>AslZeropageXCycle2(),
               Timing.T4 => AslZeropageXCycle3(),
               Timing.T5 => AslZeropageXCycle4(),
               Timing.T6 =>AslZeropageXCycle5(),
               Timing.T0 => AslZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x16)
            },       
            // 0x17 SLO zeropage
            0x17 => instructionTimer switch
            {
               Timing.T2 => SloZeropageCycle1(),
               Timing.T3 =>SloZeropageCycle2(),
               Timing.T4 => SloZeropageCycle3(),
               Timing.T5 => SloZeropageCycle4(),
               Timing.T6 =>SloZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0x17)
            },       
            // 0x18 Clc
            0x18 => instructionTimer switch
            {
               Timing.T2 => ClcCycle1(),
               Timing.T3 =>ClcCycle2(),
                _ => throw new InvalidInstructionStepException(0x18)
            },       
            // 0x19 ORA absolute, Y
            0x19 => instructionTimer switch
            {
                Timing.T2 => AbsoluteCycle1(),
                Timing.T3 => AbsoluteAddIndexYLow(Timing.T0, Timing.T4),
                Timing.T4 => AbsoluteAddIndexYHigh(),
                Timing.T0 => ReadDataPointer(),
                Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x19)
            },       
            // 0x1A NOP implied (illegal)
            0x1A => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T0,
               Timing.TPlus | Timing.T0 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x1A)
            },       
            // 0x1B SLO abs, Y (illegal)
            0x1B => instructionTimer switch
            {
               Timing.T2 => SloAbsoluteYCycle1(),
               Timing.T3 =>SloAbsoluteYCycle2(),
               Timing.T4 => SloAbsoluteYCycle3(),
               Timing.T5 => SloAbsoluteYCycle4(),
               Timing.T6 =>SloAbsoluteYCycle5(),
               Timing.T0 => SloAbsoluteYCycle6(),
               Timing.TPlus => SloAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x1B)
            },       
            // 0x1C NOP abs, X (illegal)
            0x1C => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(),
               Timing.T3 =>NopAbsoluteXCycle2(),
               Timing.T4 => NopAbsoluteXCycle3(),
               Timing.T5 => NopAbsoluteXCycle4(),
               Timing.T6 =>NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x1C)
            },       
            // 0x1D ORA abs, X
            0x1D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexXLow(Timing.T0, Timing.T4),
               Timing.T4 => AbsoluteAddIndexXHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x1D)
            },       
            // 0x1E ASL abs, X
            0x1E => instructionTimer switch
            {
               Timing.T2 => AslAbsoluteXCycle1(),
               Timing.T3 =>AslAbsoluteXCycle2(),
               Timing.T4 => AslAbsoluteXCycle3(),
               Timing.T5 => AslAbsoluteXCycle4(),
               Timing.T6 =>AslAbsoluteXCycle5(),
               Timing.T0 => AslAbsoluteXCycle6(),
               Timing.TPlus => AslAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x1E)
            },       
            // 0x1F SLO abs, X (illegal)
            0x1F => instructionTimer switch
            {
               Timing.T2 => SloAbsoluteXCycle1(),
               Timing.T3 =>SloAbsoluteXCycle2(),
               Timing.T4 => SloAbsoluteXCycle3(),
               Timing.T5 => SloAbsoluteXCycle4(),
               Timing.T6 =>SloAbsoluteXCycle5(),
               Timing.T0 => SloAbsoluteXCycle6(),
               Timing.TPlus => SloAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x1F)
            },       
            // 0x20 JSR
            0x20 => instructionTimer switch
            {
               Timing.T2 => JsrCycle1(),
               Timing.T3 =>JsrCycle2(),
               Timing.T4 => JsrCycle3(),
               Timing.T5 => JsrCycle4(),
               Timing.T6 =>JsrCycle5(),
               Timing.T0 => JsrCycle6(),
                _ => throw new InvalidInstructionStepException(0x20)
            },       
            // 0x21 AND X, indirect
            0x21 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectXCycle2(),
               Timing.T4 => IndirectXCycle3(),
               Timing.T5 => IndirectXCycle4(),
               Timing.T0 => IndirectXCycle5(false),
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x21)
            },       
            // 0x22 JAM (illegal)
            0x22 => Jam(),
            // 0x23 RLA X, indirect
            0x23 => instructionTimer switch
            {
               Timing.T2 => RlaIndirectXCycle1(),
               Timing.T3 =>RlaIndirectXCycle2(),
               Timing.T4 => RlaIndirectXCycle3(),
               Timing.T5 => RlaIndirectXCycle4(),
               Timing.T6 =>RlaIndirectXCycle5(),
               Timing.T0 => RlaIndirectXCycle6(),
               Timing.TPlus => RlaIndirectXCycle7(),
               Timing.T8 => RlaIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0x23)
            },       
            // 0x24 BIT zeropage
            0x24 => instructionTimer switch
            {
               Timing.T2 => BitZeropageCycle1(),
               Timing.T3 => BitZeropageCycle2(),
               Timing.TPlus | Timing.T1 => Bit(),
                _ => throw new InvalidInstructionStepException(0x24)
            },       
            // 0x25 AND zeropage
            0x25 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(), 
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x25)
            },       
            // 0x26 ROL zeropage
            0x26 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(), 
               Timing.T3 | Timing.SD1 => ZeropageRead(),
               Timing.T4 | Timing.SD2 => Rol(),
               Timing.T0 => RolZeropageCycle4(),
               Timing.TPlus | Timing.T1 =>RolZeropageCycle5(),
               _ => throw new InvalidInstructionStepException(0x26)
            },       
            // 0x27 RLA zeropage (illegal)
            0x27 => instructionTimer switch
            {
               Timing.T2 => RlaZeropageCycle1(), 
               Timing.T3 =>RlaZeropageCycle2(), 
               Timing.T4 => RlaZeropageCycle3(), 
               Timing.T5 => RlaZeropageCycle4(), 
               Timing.T6 =>RlaZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x27)
            },
            // 0x28 PHA
            0x28 => instructionTimer switch
            {
               Timing.T2 => PlpCycle1(), 
               Timing.T3 =>PlpCycle2(), 
               Timing.T4 => PlpCycle3(), 
               Timing.T5 => PlpCycle4(), 
                _ => throw new InvalidInstructionStepException(0x28)
            },
            // 0x29 AND imd
            0x29 => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.TPlus | Timing.T1, 
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister), 
                _ => throw new InvalidInstructionStepException(0x29)
            },
            // 0x2A ROL accum
            0x2A => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.TPlus | Timing.T1, 
               Timing.TPlus | Timing.T0 => Rol(true), 
                _ => throw new InvalidInstructionStepException(0x2A)
            },
            // 0x2B ANC (illegal)
            0x2B => instructionTimer switch
            {
               Timing.T2 => AncCycle1(), 
               Timing.T3 =>AncCycle2(), 
               Timing.T4 => AncCycle3(), 
                _ => throw new InvalidInstructionStepException(0x2B)
            },
            // 0x2C BIT abs
            0x2C => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteCycle2(Timing.T0), 
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T1), 
               Timing.TPlus | Timing.T1 => Bit(), 
                _ => throw new InvalidInstructionStepException(0x2C)
            },
            // 0x2D AND abs
            0x2D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteCycle2(Timing.T0), 
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T1), 
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x2D)
            },
            // 0x2E ROL abs
            0x2E => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteCycle2(), 
               Timing.T4 | Timing.SD1 => ReadDataPointer(), 
               Timing.T5 | Timing.SD2 => Rol(), 
               Timing.T0 => Write(MemoryAddressRegister, TempRegister), 
               Timing.TPlus | Timing.T1 => Timing.T2, 
                _ => throw new InvalidInstructionStepException(0x2E)
            },
            // 0x2F RLA abs (illegal)
            0x2F => instructionTimer switch
            {
               Timing.T2 => RlaAbsoluteCycle1(), 
               Timing.T3 =>RlaAbsoluteCycle2(), 
               Timing.T4 => RlaAbsoluteCycle3(), 
               Timing.T5 => RlaAbsoluteCycle4(), 
               Timing.T6 =>RlaAbsoluteCycle5(), 
               Timing.T0 => RlaAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x2F)
            },
            // 0x30 BMI
            0x30 => instructionTimer switch
            {
               Timing.T2 => BmiCycle1(), 
               Timing.T3 =>BmiCycle2(), 
               Timing.T4 => BmiCycle3(), 
               Timing.T5 => BmiCycle4(), 
                _ => throw new InvalidInstructionStepException(0x30)
            },
            // 0x31 AND ind, Y
            0x31 => instructionTimer switch
            {
                Timing.T2 => IndirectCycle1(),
                Timing.T3 => IndirectYCycle2(),
                Timing.T4 => IndirectYCycle3(),
                Timing.T5 => IndirectYCycle4(),
                Timing.T0 => IndirectYCycle5(),
                Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x31)
            },
            // 0x32 JAM (illegal)
            0x32 => Jam(),
            // 0x33 RLA ind, Y (illegal)
            0x33 => instructionTimer switch
            {
               Timing.T2 => RlaIndirectYCycle1(), 
               Timing.T3 =>RlaIndirectYCycle2(), 
               Timing.T4 => RlaIndirectYCycle3(), 
               Timing.T5 => RlaIndirectYCycle4(),  
               Timing.T6 =>RlaIndirectYCycle5(), 
               Timing.T0 => RlaIndirectYCycle6(), 
               Timing.TPlus => RlaIndirectYCycle7(), 
               Timing.T8 => RlaIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x33)
            },
            // 0x34 NOP zeropage, X
            0x34 => instructionTimer switch
            {
               Timing.T2 => NopZeropageXCycle1(), 
               Timing.T3 =>NopZeropageXCycle2(), 
               Timing.T4 => NopZeropageXCycle3(), 
               Timing.T5 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x34)
            },
            // 0x35 AND zeropage, X
            0x35 => instructionTimer switch
            {
               Timing.T2 => AndZeropageXCycle1(), 
               Timing.T3 =>AndZeropageXCycle2(), 
               Timing.T4 => AndZeropageXCycle3(), 
               Timing.T5 => AndZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x35)
            },
            // 0x36 ROL zeropage, X
            0x36 => instructionTimer switch
            {
               Timing.T2 => RolZeropageXCycle1(), 
               Timing.T3 =>RolZeropageXCycle2(), 
               Timing.T4 => RolZeropageXCycle3(), 
               Timing.T5 => RolZeropageXCycle4(), 
               Timing.T6 =>RolZeropageXCycle5(), 
               Timing.T0 => RolZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x36)
            },
            // 0x37 RLA zeropage, X (illegal)
            0x37 => instructionTimer switch
            {
               Timing.T2 => RlaZeropageXCycle1(), 
               Timing.T3 =>RlaZeropageXCycle2(), 
               Timing.T4 => RlaZeropageXCycle3(), 
               Timing.T5 => RlaZeropageXCycle4(), 
               Timing.T6 =>RlaZeropageXCycle5(), 
               Timing.T0 => RlaZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x37)
            },
            // 0x38 SEC implied
            0x38 => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => SecCycle1(), 
               Timing.TPlus =>SecCycle2(), 
                _ => throw new InvalidInstructionStepException(0x38)
            },
            // 0x39 And abs, Y
            0x39 => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexYLow(Timing.T0, Timing.T4),
               Timing.T4 => AbsoluteAddIndexYHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x39)
            },
            // 0x3A NOP implied (illegal)
            0x3A => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(), 
               Timing.T3 => NopImpliedCycle2(), 
                _ => throw new InvalidInstructionStepException(0x3A)
            },
            // 0x3B RLA abs, Y (illegal)
            0x3B => instructionTimer switch
            {
               Timing.T2 => RlaAbsoluteYCycle1(), 
               Timing.T3 =>RlaAbsoluteYCycle2(), 
               Timing.T4 => RlaAbsoluteYCycle3(), 
               Timing.T5 => RlaAbsoluteYCycle4(), 
               Timing.T6 =>RlaAbsoluteYCycle5(),
               Timing.T0 => RlaAbsoluteYCycle6(),
               Timing.TPlus => RlaAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x3B)
            },
            // 0x3C NOP abs, X (illegal)
            0x3C => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(), 
               Timing.T3 =>NopAbsoluteXCycle2(), 
               Timing.T4 => NopAbsoluteXCycle3(), 
               Timing.T5 => NopAbsoluteXCycle4(), 
               Timing.T6 =>NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x3C)
            },
            // 0x3D AND abs, X
            0x3D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexXLow(Timing.T0, Timing.T4),
               Timing.T4 => AbsoluteAddIndexXHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => LogicAnd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x3D)
            },
            // 0x3E ROL abs, X
            0x3E => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteAddIndexXLow(Timing.T5 | Timing.SD1, Timing.T4), 
               Timing.T4 => AbsoluteAddIndexXHigh(Timing.T5 | Timing.SD1), 
               Timing.T5 | Timing.SD1 => ReadDataPointer(Timing.SD2), 
               Timing.SD2 => Rol(), 
               Timing.T0 => Write(MemoryDataRegister, TempRegister), 
               Timing.TPlus | Timing.T1 => Timing.T2, 
                _ => throw new InvalidInstructionStepException(0x3E)
            },
            // 0x3F RLA abs, X (illegal)
            0x3F => instructionTimer switch
            {
               Timing.T2 => RlaAbsoluteXCycle1(), 
               Timing.T3 =>RlaAbsoluteXCycle2(), 
               Timing.T4 => RlaAbsoluteXCycle3(), 
               Timing.T5 => RlaAbsoluteXCycle4(), 
               Timing.T6 =>RlaAbsoluteXCycle5(), 
               Timing.T0 => RlaAbsoluteXCycle6(), 
               Timing.TPlus => RlaAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x3F)
            },
            // 0x40 RTI implied
            0x40 => instructionTimer switch
            {
               Timing.T2 => RtiCycle1(), 
               Timing.T3 =>RtiCycle2(), 
               Timing.T4 => RtiCycle3(), 
               Timing.T5 => RtiCycle4(), 
               Timing.T6 =>RtiCycle5(), 
               Timing.T0 => RtiCycle6(), 
                _ => throw new InvalidInstructionStepException(0x40)
            },
            // 0x41 EOR indirect, X
            0x41 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectXCycle2(),
               Timing.T4 => IndirectXCycle3(),
               Timing.T5 => IndirectXCycle4(),
               Timing.T0 => IndirectXCycle5(false),
               Timing.TPlus => LogicOr(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x41)
            },
            // 0x42 JAM (illegal)
            0x42 => Jam(),
            // 0x43 Sre indirect, X
            0x43 => instructionTimer switch
            {
               Timing.T2 => SreIndirectXCycle1(), 
               Timing.T3 =>SreIndirectXCycle2(), 
               Timing.T4 => SreIndirectXCycle3(), 
               Timing.T5 => SreIndirectXCycle4(), 
               Timing.T6 =>SreIndirectXCycle5(), 
               Timing.T0 => SreIndirectXCycle6(), 
               Timing.TPlus => SreIndirectXCycle7(), 
               Timing.T8 => SreIndirectXCycle8(), 
                _ => throw new InvalidInstructionStepException(0x43)
            },
            // 0x44 NOP zeropage (illegal)
            0x44 => instructionTimer switch
            {
               Timing.T2 => NopZeropageCycle1(), 
               Timing.T3 =>NopZeropageCycle2(), 
               Timing.T4 => NopZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x44)
            },
            // 0x45 EOR zeropage
            0x45 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(), 
               Timing.T3 => ZeropageRead(), 
               Timing.T4 => LogicXor(MemoryDataRegister), 
                _ => throw new InvalidInstructionStepException(0x45)
            },
            // 0x46 LSR zeropage
            0x46 => instructionTimer switch
            {
               Timing.T2 => LsrZeropageCycle1(), 
               Timing.T3 =>LsrZeropageCycle2(), 
               Timing.T4 => LsrZeropageCycle3(), 
               Timing.T5 => LsrZeropageCycle4(), 
               Timing.T6 =>LsrZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x46)
            },
            // 0x47 SRE zeropage (illegal)
            0x47 => instructionTimer switch
            {
               Timing.T2 => SreZeropageCycle1(), 
               Timing.T3 =>SreZeropageCycle2(), 
               Timing.T4 => SreZeropageCycle3(), 
               Timing.T5 => SreZeropageCycle4(), 
               Timing.T6 =>SreZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x47)
            },
            // 0x48 PHA imp
            0x48 => instructionTimer switch
            {
               Timing.T2 => PhaCycle1(), 
               Timing.T3 =>PhaCycle2(), 
               Timing.T4 => PhaCycle3(), 
                _ => throw new InvalidInstructionStepException(0x48)
            },
            // 0x49 EOR imm
            0x49 => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1, 
               Timing.TPlus | Timing.T1 => LogicXor(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x49)
            },
            // 0x4A LSR accum
            0x4A => instructionTimer switch
            {
               Timing.T2 => LsrAccumCycle1(), 
               Timing.T3 =>LsrAccumCycle2(), 
                _ => throw new InvalidInstructionStepException(0x4A)
            },
            // 0x4B ALR (illegal)
            0x4B => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.T3,
               Timing.T3 => Alr(), 
                _ => throw new InvalidInstructionStepException(0x4B)
            },
            // 0x4C JMP abs
            0x4C => instructionTimer switch
            {
               Timing.T2 => JmpAbsoluteCycle1(), 
               Timing.T3 =>JmpAbsoluteCycle2(), 
               Timing.T4 => JmpAbsoluteCycle3(), 
               Timing.T5 => JmpAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x4C)
            },
            // 0x4D EOR abs
            0x4D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteCycle2(), 
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T1), 
               Timing.T5 => LogicXor(MemoryDataRegister), 
                _ => throw new InvalidInstructionStepException(0x4D)
            },
            // 0x4E LSR abs
            0x4E => instructionTimer switch
            {
               Timing.T2 => LsrAbsoluteCycle1(), 
               Timing.T3 =>LsrAbsoluteCycle2(), 
               Timing.T4 => LsrAbsoluteCycle3(), 
               Timing.T5 => LsrAbsoluteCycle4(), 
               Timing.T6 =>LsrAbsoluteCycle5(), 
               Timing.T0 => LsrAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x4E)
            },
            // 0x4F SRE abs (illegal)
            0x4F => instructionTimer switch
            {
               Timing.T2 => SreAbsoluteCycle1(), 
               Timing.T3 =>SreAbsoluteCycle2(), 
               Timing.T4 => SreAbsoluteCycle3(), 
               Timing.T5 => SreAbsoluteCycle4(), 
               Timing.T6 =>SreAbsoluteCycle5(), 
               Timing.T0 => SreAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x4F)
            },
            // 0x50 BVC rel
            0x50 => instructionTimer switch
            {
               Timing.T2 => BvcCycle1(), 
               Timing.T3 =>BvcCycle2(), 
               Timing.T4 => BvcCycle3(), 
               Timing.T5 => BvcCycle4(), 
                _ => throw new InvalidInstructionStepException(0x50)
            },
            // 0x51 BVC rel
            0x51 => instructionTimer switch
            {
               Timing.T2 => EorIndirectYCycle1(), 
               Timing.T3 =>EorIndirectYCycle2(), 
               Timing.T4 => EorIndirectYCycle3(), 
               Timing.T5 => EorIndirectYCycle4(), 
               Timing.T6 =>EorIndirectYCycle5(), 
               Timing.T0 => EorIndirectYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x51)
            },
            // 0x52 JAM
            0x52 => Jam(),
            // 0x53 SRE indirect, Y
            0x53 => instructionTimer switch
            {
               Timing.T2 => SreIndirectYCycle1(), 
               Timing.T3 =>SreIndirectYCycle2(), 
               Timing.T4 => SreIndirectYCycle3(), 
               Timing.T5 => SreIndirectYCycle4(), 
               Timing.T6 =>SreIndirectYCycle5(), 
               Timing.T0 => SreIndirectYCycle6(), 
               Timing.TPlus => SreIndirectYCycle7(), 
               Timing.T8 => SreIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x53)
            },
            // 0x54 NOP zeropage, X
            0x54 => instructionTimer switch
            {
               Timing.T2 => NopZeropageXCycle1(), 
               Timing.T3 =>NopZeropageXCycle2(), 
               Timing.T4 => NopZeropageXCycle3(), 
               Timing.T5 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x54)
            },
            // 0x55 EOR zeropage, X
            0x55 => instructionTimer switch
            {
               Timing.T2 => EorZeropageXCycle1(), 
               Timing.T3 =>EorZeropageXCycle2(), 
               Timing.T4 => EorZeropageXCycle3(), 
               Timing.T5 => EorZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x55)
            },
            // 0x56 LSR zeropage, X
            0x56 => instructionTimer switch
            {
               Timing.T2 => LsrZeropageXCycle1(), 
               Timing.T3 =>LsrZeropageXCycle2(), 
               Timing.T4 => LsrZeropageXCycle3(), 
               Timing.T5 => LsrZeropageXCycle4(), 
               Timing.T6 =>LsrZeropageXCycle5(), 
               Timing.T0 => LsrZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x56)
            },
            // 0x57 SRE zeropage, X
            0x57 => instructionTimer switch
            {
               Timing.T2 => SreZeropageXCycle1(), 
               Timing.T3 =>SreZeropageXCycle2(), 
               Timing.T4 => SreZeropageXCycle3(), 
               Timing.T5 => SreZeropageXCycle4(), 
               Timing.T6 =>SreZeropageXCycle5(), 
               Timing.T0 => SreZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x57)
            },
            // 0x58 CLI impl
            0x58 => instructionTimer switch
            {
               Timing.T2 => CliCycle1(), 
               Timing.T3 =>CliCycle2(), 
                _ => throw new InvalidInstructionStepException(0x58)
            },
            // 0x59 EOR abs, Y
            0x59 => instructionTimer switch
            {
               Timing.T2 => EorAbsoluteYCycle1(), 
               Timing.T3 =>EorAbsoluteYCycle2(), 
               Timing.T4 => EorAbsoluteYCycle3(), 
               Timing.T5 => EorAbsoluteYCycle4(), 
               Timing.T6 =>EorAbsoluteYCycle5(), 
                _ => throw new InvalidInstructionStepException(0x59)
            },
            // 0x5A NOP impl
            0x5A => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(), 
               Timing.T3 =>NopImpliedCycle2(), 
                _ => throw new InvalidInstructionStepException(0x5A)
            },
            // 0x5B SRE abs, Y
            0x5B => instructionTimer switch
            {
               Timing.T2 => SreAbsoluteYCycle1(), 
               Timing.T3 =>SreAbsoluteYCycle2(), 
               Timing.T4 => SreAbsoluteYCycle3(), 
               Timing.T5 => SreAbsoluteYCycle4(), 
               Timing.T6 =>SreAbsoluteYCycle5(), 
               Timing.T0 => SreAbsoluteYCycle6(), 
               Timing.TPlus => SreAbsoluteYCycle7(), 
                _ => throw new InvalidInstructionStepException(0x5B)
            },
            // 0x5C NOP abs, X
            0x5C => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(), 
               Timing.T3 =>NopAbsoluteXCycle2(), 
               Timing.T4 => NopAbsoluteXCycle3(), 
               Timing.T5 => NopAbsoluteXCycle4(), 
               Timing.T6 =>NopAbsoluteXCycle5(), 
                _ => throw new InvalidInstructionStepException(0x5C)
            },
            // 0x5D EOR abs, X
            0x5D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexXLow(Timing.T0, Timing.T4),
               Timing.T4 => AbsoluteAddIndexXHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => LogicXor(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x5D)
            },
            // 0x5E LSR abs, X
            0x5E => instructionTimer switch
            {
               Timing.T2 => LsrAbsoluteXCycle1(), 
               Timing.T3 =>LsrAbsoluteXCycle2(), 
               Timing.T4 => LsrAbsoluteXCycle3(), 
               Timing.T5 => LsrAbsoluteXCycle4(), 
               Timing.T6 =>LsrAbsoluteXCycle5(), 
               Timing.T0 => LsrAbsoluteXCycle6(), 
               Timing.TPlus => LsrAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x5E)
            },
            // 0x5F SRE abs, X
            0x5F => instructionTimer switch
            {
               Timing.T2 => SreAbsoluteXCycle1(), 
               Timing.T3 =>SreAbsoluteXCycle2(), 
               Timing.T4 => SreAbsoluteXCycle3(), 
               Timing.T5 => SreAbsoluteXCycle4(), 
               Timing.T6 =>SreAbsoluteXCycle5(), 
               Timing.T0 => SreAbsoluteXCycle6(), 
               Timing.TPlus => SreAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x5F)
            },
            // 0x60 RTS impl
            0x60 => instructionTimer switch
            {
               Timing.T2 => RtsCycle1(), 
               Timing.T3 =>RtsCycle2(), 
               Timing.T4 => RtsCycle3(), 
               Timing.T5 => RtsCycle4(), 
               Timing.T6 =>RtsCycle5(), 
               Timing.T0 => RtsCycle6(), 
                _ => throw new InvalidInstructionStepException(0x60)
            },
            // 0x61 ADC indirect, X
            0x61 => instructionTimer switch
            {
               Timing.T2 => AdcIndirectXCycle1(), 
               Timing.T3 =>AdcIndirectXCycle2(), 
               Timing.T4 => AdcIndirectXCycle3(), 
               Timing.T5 => AdcIndirectXCycle4(), 
               Timing.T6 =>AdcIndirectXCycle5(), 
               Timing.T0 => AdcIndirectXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x61)
            },
            // 0x62 JAM
            0x62 => Jam(),
            // 0x63 RRA X, indirect
            0x63 => instructionTimer switch
            {
               Timing.T2 => RraIndirectXCycle1(), 
               Timing.T3 =>RraIndirectXCycle2(), 
               Timing.T4 => RraIndirectXCycle3(), 
               Timing.T5 => RraIndirectXCycle4(), 
               Timing.T6 =>RraIndirectXCycle5(), 
               Timing.T0 => RraIndirectXCycle6(), 
               Timing.TPlus => RraIndirectXCycle7(), 
               Timing.T8 => RraIndirectXCycle8(), 
                _ => throw new InvalidInstructionStepException(0x63)
            },
            // 0x64 NOP zeropage
            0x64 => instructionTimer switch
            {
               Timing.T2 => NopZeropageXCycle1(), 
               Timing.T3 =>NopZeropageXCycle2(), 
               Timing.T4 => NopZeropageXCycle3(), 
               Timing.T5 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x64)
            },
            // 0x65 ADC zeropage
            0x65 => instructionTimer switch
            {
               Timing.T2 => AdcZeropageCycle1(), 
               Timing.T3 =>AdcZeropageCycle2(), 
               Timing.T4 => AdcZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x65)
            },
            // 0x66 ROR zeropage
            0x66 => instructionTimer switch
            {
               Timing.T2 => RorZeropageCycle1(), 
               Timing.T3 =>RorZeropageCycle2(), 
               Timing.T4 => RorZeropageCycle3(), 
               Timing.T5 => RorZeropageCycle4(), 
               Timing.T6 =>RorZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x66)
            },
            // 0x67 RRA zeropage
            0x67 => instructionTimer switch
            {
               Timing.T2 => RraZeropageCycle1(), 
               Timing.T3 =>RraZeropageCycle2(), 
               Timing.T4 => RraZeropageCycle3(), 
               Timing.T5 => RraZeropageCycle4(), 
               Timing.T6 =>RraZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x67)
            },
            // 0x68 PLA impl
            0x68 => instructionTimer switch
            {
               Timing.T2 => PlaCycle1(), 
               Timing.T3 =>PlaCycle2(), 
               Timing.T4 => PlaCycle3(), 
               Timing.T5 => PlaCycle4(), 
                _ => throw new InvalidInstructionStepException(0x68)
            },
            // 0x69 ADC imm
            0x69 => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.T3,
               Timing.T3 => LogicAdd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x69)
            },
            // 0x6A ROR abs
            0x6A => instructionTimer switch
            {
               Timing.T2 => RorAccumCycle1(), 
               Timing.T3 =>RorAccumCycle2(), 
                _ => throw new InvalidInstructionStepException(0x6A)
            },
            // 0x6B Arr imm
            0x6B => instructionTimer switch
            {
               Timing.T2 => ArrCycle1(), 
               Timing.T3 =>ArrCycle2(), 
                _ => throw new InvalidInstructionStepException(0x6B)
            },
            // 0x6C JMP indirect
            0x6C => instructionTimer switch
            {
               Timing.T2 => JmpIndirectCycle1(), 
               Timing.T3 =>JmpIndirectCycle2(), 
               Timing.T4 => JmpIndirectCycle3(), 
               Timing.T5 => JmpIndirectCycle4(), 
               Timing.T6 =>JmpIndirectCycle5(), 
                _ => throw new InvalidInstructionStepException(0x6C)
            },
            // 0x6D ADC abs
            0x6D => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(), 
               Timing.T3 => AbsoluteCycle2(Timing.T4), 
               Timing.T4 => ReadDataPointer(Timing.T5), 
               Timing.T5 => LogicAdd(MemoryDataRegister), 
                _ => throw new InvalidInstructionStepException(0x6D)
            },
            // 0x6E ROR abs
            0x6E => instructionTimer switch
            {
               Timing.T2 => RorAbsoluteCycle1(), 
               Timing.T3 =>RorAbsoluteCycle2(), 
               Timing.T4 => RorAbsoluteCycle3(), 
               Timing.T5 => RorAbsoluteCycle4(), 
               Timing.T6 =>RorAbsoluteCycle5(), 
               Timing.T0 => RorAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x6E)
            },
            // 0x6F RRA abs
            0x6F => instructionTimer switch
            {
               Timing.T2 => RraAbsoluteCycle1(), 
               Timing.T3 =>RraAbsoluteCycle2(), 
               Timing.T4 => RraAbsoluteCycle3(), 
               Timing.T5 => RraAbsoluteCycle4(), 
               Timing.T6 =>RraAbsoluteCycle5(), 
               Timing.T0 => RraAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x6F)
            },
            // 0x70 BVS rel
            0x70 => instructionTimer switch
            {
               Timing.T2 => BvsCycle1(), 
               Timing.T3 =>BvsCycle2(), 
               Timing.T4 => BvsCycle3(), 
               Timing.T5 => BvsCycle4(), 
                _ => throw new InvalidInstructionStepException(0x70)
            },
            // 0x71 ADC ind, Y
            0x71 => instructionTimer switch
            {
               Timing.T2 => AdcIndirectYCycle1(), 
               Timing.T3 =>AdcIndirectYCycle2(), 
               Timing.T4 => AdcIndirectYCycle3(), 
               Timing.T5 => AdcIndirectYCycle4(), 
               Timing.T6 =>AdcIndirectYCycle5(), 
               Timing.T0 => AdcIndirectYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x71)
            },
            // 0x72 JAM
            0x72 => Jam(),
            // 0x73 RRA ind, Y
            0x73 => instructionTimer switch
            {
               Timing.T2 => RraIndirectYCycle1(), 
               Timing.T3 =>RraIndirectYCycle2(), 
               Timing.T4 => RraIndirectYCycle3(), 
               Timing.T5 => RraIndirectYCycle4(), 
               Timing.T6 =>RraIndirectYCycle5(), 
               Timing.T0 => RraIndirectYCycle6(), 
               Timing.TPlus => RraIndirectYCycle7(), 
               Timing.T8 => RraIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x73)
            },
            // 0x74 NOP zeropage, X
            0x74 => instructionTimer switch
            {
               Timing.T2 => NopZeropageCycle1(),
               Timing.T3 =>NopZeropageCycle2(),
               Timing.T4 => NopZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x74)
            },       
            // 0x75 ADC zeropage, X
            0x75 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 => ZeropageAddIndexXLow(),
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => LogicAdd(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0x75)
            },       
            // 0x76 ROR zeropage, X
            0x76 => instructionTimer switch
            {
               Timing.T2 => RorZeropageXCycle1(),
               Timing.T3 =>RorZeropageXCycle2(),
               Timing.T4 => RorZeropageXCycle3(),
               Timing.T5 => RorZeropageXCycle4(),
               Timing.T6 =>RorZeropageXCycle5(),
               Timing.T0 => RorZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x76)
            },       
            // 0x77 RRA zeropage, X
            0x77 => instructionTimer switch
            {
               Timing.T2 => RraZeropageXCycle1(),
               Timing.T3 =>RraZeropageXCycle2(),
               Timing.T4 => RraZeropageXCycle3(),
               Timing.T5 => RraZeropageXCycle4(),
               Timing.T6 =>RraZeropageXCycle5(),
               Timing.T0 => RraZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x77)
            },       
            // 0x78 SEI impl
            0x78 => instructionTimer switch
            {
               Timing.T2 => SeiCycle1(),
               Timing.T3 =>SeiCycle2(),
                _ => throw new InvalidInstructionStepException(0x78)
            },       
            // 0x79 ADC abs, Y
            0x79 => instructionTimer switch
            {
               Timing.T2 => AdcAbsoluteYCycle1(),
               Timing.T3 =>AdcAbsoluteYCycle2(),
               Timing.T4 => AdcAbsoluteYCycle3(),
               Timing.T5 => AdcAbsoluteYCycle4(),
               Timing.T6 =>AdcAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x79)
            },       
            // 0x7A NOP impl
            0x7A => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(),
               Timing.T3 =>NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0x7A)
            },       
            // 0x7B RRA abs, Y
            0x7B => instructionTimer switch
            {
               Timing.T2 => RraAbsoluteYCycle1(),
               Timing.T3 =>RraAbsoluteYCycle2(),
               Timing.T4 => RraAbsoluteYCycle3(),
               Timing.T5 => RraAbsoluteYCycle4(),
               Timing.T6 =>RraAbsoluteYCycle5(),
               Timing.T0 => RraAbsoluteYCycle6(),
               Timing.TPlus => RraAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x7B)
            },       
            // 0x7C NOP abs, X
            0x7C => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(),
               Timing.T3 =>NopAbsoluteXCycle2(),
               Timing.T4 => NopAbsoluteXCycle3(),
               Timing.T5 => NopAbsoluteXCycle4(),
               Timing.T6 =>NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x7C)
            },       
            // 0x7D ADC abs, X
            0x7D => instructionTimer switch
            {
               Timing.T2 => AdcAbsoluteXCycle1(),
               Timing.T3 =>AdcAbsoluteXCycle2(),
               Timing.T4 => AdcAbsoluteXCycle3(),
               Timing.T5 => AdcAbsoluteXCycle4(),
               Timing.T6 =>AdcAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x7D)
            },       
            // 0x7E ROR abs, X
            0x7E => instructionTimer switch
            {
               Timing.T2 => RorAbsoluteXCycle1(),
               Timing.T3 =>RorAbsoluteXCycle2(),
               Timing.T4 => RorAbsoluteXCycle3(),
               Timing.T5 => RorAbsoluteXCycle4(),
               Timing.T6 =>RorAbsoluteXCycle5(),
               Timing.T0 => RorAbsoluteXCycle6(),
               Timing.TPlus => RorAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x7E)
            },       
            // 0x7F RRA abs, X
            0x7F => instructionTimer switch
            {
               Timing.T2 => RraAbsoluteXCycle1(),
               Timing.T3 =>RraAbsoluteXCycle2(),
               Timing.T4 => RraAbsoluteXCycle3(),
               Timing.T5 => RraAbsoluteXCycle4(),
               Timing.T6 =>RraAbsoluteXCycle5(),
               Timing.T0 => RraAbsoluteXCycle6(),
               Timing.TPlus => RraAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x7F)
            },       
            // 0x80 NOP imm
            0x80 => instructionTimer switch
            {
               Timing.T0 |  Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x80)
            },       
            // 0x81 STA X, ind
            0x81 => instructionTimer switch
            {
               Timing.T2 => StaIndirectXCycle1(),
               Timing.T3 =>StaIndirectXCycle2(),
               Timing.T4 => StaIndirectXCycle3(),
               Timing.T5 => StaIndirectXCycle4(),
               Timing.T6 =>StaIndirectXCycle5(),
               Timing.T0 => StaIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x81)
            },       
            // 0x82 NOP imm
            0x82 => instructionTimer switch
            {
               Timing.T0 |  Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x82)
            },       
            // 0x83 SAX x, ind
            0x83 => instructionTimer switch
            {
               Timing.T2 => SaxIndirectXCycle1(),
               Timing.T3 =>SaxIndirectXCycle2(),
               Timing.T4 => SaxIndirectXCycle3(),
               Timing.T5 => SaxIndirectXCycle4(),
               Timing.T6 =>SaxIndirectXCycle5(),
               Timing.T0 => SaxIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x83)
            },       
            // 0x84 STY zpg
            0x84 => instructionTimer switch
            {
               Timing.T2 => StyZeropageCycle1(),
               Timing.T3 =>StyZeropageCycle2(),
               Timing.T4 => StyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x84)
            },       
            // 0x85 STA zpg
            0x85 => instructionTimer switch
            {
               Timing.T2 => StaZeropageCycle1(),
               Timing.T3 =>StaZeropageCycle2(),
               Timing.T4 => StaZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x85)
            },       
            // 0x86 STX zpg
            0x86 => instructionTimer switch
            {
               Timing.T2 => StxZeropageCycle1(),
               Timing.T3 =>StxZeropageCycle2(),
               Timing.T4 => StxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x86)
            },       
            // 0x87 SAX zpg
            0x87 => instructionTimer switch
            {
               Timing.T2 => SaxZeropageCycle1(),
               Timing.T3 =>SaxZeropageCycle2(),
               Timing.T4 => SaxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x87)
            },       
            // 0x88 DEY impl
            0x88 => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => DeyCycle2(),
                _ => throw new InvalidInstructionStepException(0x88)
            },       
            // 0x89 NOP impl
            0x89 => instructionTimer switch
            {
               Timing.T0 |  Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0x89)
            },       
            // 0x8A TXA impl
            0x8A => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.TPlus | Timing.T0,
               Timing.TPlus | Timing.T1 => Transfer(X, DestinationRegister.Accumulator),
                _ => throw new InvalidInstructionStepException(0x8A)
            },       
            // 0x8B ANE impl
            0x8B => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => AneCycle2(),
                _ => throw new InvalidInstructionStepException(0x8B)
            },       
            // 0x8C STY abs
            0x8C => instructionTimer switch
            {
               Timing.T2 => StyAbsoluteCycle1(),
               Timing.T3 =>StyAbsoluteCycle2(),
               Timing.T4 => StyAbsoluteCycle3(),
               Timing.T5 => StyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8C)
            },       
            // 0x8D STA abs
            0x8D => instructionTimer switch
            {
               Timing.T2 => StaAbsoluteCycle1(),
               Timing.T3 =>StaAbsoluteCycle2(),
               Timing.T4 => StaAbsoluteCycle3(),
               Timing.T5 => StaAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8D)
            },       
            // 0x8E STX abs
            0x8E => instructionTimer switch
            {
               Timing.T2 => StxAbsoluteCycle1(),
               Timing.T3 =>StxAbsoluteCycle2(),
               Timing.T4 => StxAbsoluteCycle3(),
               Timing.T5 => StxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8E)
            },       
            // 0x8F STA abs
            0x8F => instructionTimer switch
            {
               Timing.T2 => SaxAbsoluteCycle1(),
               Timing.T3 =>SaxAbsoluteCycle2(),
               Timing.T4 => SaxAbsoluteCycle3(),
               Timing.T5 => SaxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8F)
            },       
            // 0x90 BCC rel
            0x90 => instructionTimer switch
            {
               Timing.T2 => BccCycle1(),
               Timing.T3 =>BccCycle2(),
               Timing.T4 => BccCycle3(),
               Timing.T5 => BccCycle4(),
                _ => throw new InvalidInstructionStepException(0x90)
            },       
            // 0x91 STA ind, Y
            0x91 => instructionTimer switch
            {
               Timing.T2 => StaIndirectYCycle1(),
               Timing.T3 =>StaIndirectYCycle2(),
               Timing.T4 => StaIndirectYCycle3(),
               Timing.T5 => StaIndirectYCycle4(),
               Timing.T6 =>StaIndirectYCycle5(),
               Timing.T0 => StaIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0x91)
            },       
            // 0x92 Sta ind, Y
            0x92 => Jam(),
            // 0x93 SHA ind, Y
            0x93 => instructionTimer switch
            {
               Timing.T2 => ShaIndirectYCycle1(),
               Timing.T3 =>ShaIndirectYCycle2(),
               Timing.T4 => ShaIndirectYCycle3(),
               Timing.T5 => ShaIndirectYCycle4(),
               Timing.T6 =>ShaIndirectYCycle5(),
                _ => throw new InvalidInstructionStepException(0x93)
            },       
            // 0x94 STY zpg, X
            0x94 => instructionTimer switch
            {
               Timing.T2 => StyZeropageXCycle1(),
               Timing.T3 =>StyZeropageXCycle2(),
               Timing.T4 => StyZeropageXCycle3(),
               Timing.T5 => StyZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x94)
            },       
            // 0x95 STA zpg, X
            0x95 => instructionTimer switch
            {
               Timing.T2 => StaZeropageXCycle1(),
               Timing.T3 =>StaZeropageXCycle2(),
               Timing.T4 => StaZeropageXCycle3(),
               Timing.T5 => StaZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x95)
            },       
            // 0x96 STX zpg, X
            0x96 => instructionTimer switch
            {
               Timing.T2 => StxZeropageYCycle1(),
               Timing.T3 =>StxZeropageYCycle2(),
               Timing.T4 => StxZeropageYCycle3(), 
               Timing.T5 => StxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0x96)
            },       
            // 0x97 SAX zpg, Y
            0x97 => instructionTimer switch
            {
               Timing.T2 => SaxZeropageYCycle1(),
               Timing.T3 =>SaxZeropageYCycle2(),
               Timing.T4 => SaxZeropageYCycle3(),
               Timing.T5 => SaxZeropageYCycle4(),
               Timing.T6 =>SaxZeropageYCycle5(),
               Timing.T0 => SaxZeropageYCycle6(),
                _ => throw new InvalidInstructionStepException(0x97)
            },       
            // 0x98 TYA impl
            0x98 => instructionTimer switch
            {
               Timing.T0 |  Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Transfer(Y, DestinationRegister.Accumulator),
                _ => throw new InvalidInstructionStepException(0x98)
            },       
            // 0x99 STA abs, Y
            0x99 => instructionTimer switch
            {
               Timing.T2 => StaAbsoluteYCycle1(),
               Timing.T3 =>StaAbsoluteYCycle2(),
               Timing.T4 => StaAbsoluteYCycle3(),
               Timing.T5 => StaAbsoluteYCycle4(),
               Timing.T6 =>StaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x99)
            },       
            // 0x9A TXS impl
            0x9A => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Transfer(X, DestinationRegister.StackPointer),
                _ => throw new InvalidInstructionStepException(0x9A)
            },       
            // 0x9B TAS abs, Y
            0x9B => instructionTimer switch
            {
               Timing.T2 => TasCycle1(),
               Timing.T3 =>TasCycle2(),
               Timing.T4 => TasCycle3(),
               Timing.T5 => TasCycle4(),
               Timing.T6 =>TasCycle5(),
                _ => throw new InvalidInstructionStepException(0x9B)
            },       
            // 0x9C SHY abs, X
            0x9C => instructionTimer switch
            {
               Timing.T2 => ShyCycle1(),
               Timing.T3 =>ShyCycle2(),
               Timing.T4 => ShyCycle3(),
               Timing.T5 => ShyCycle4(),
               Timing.T6 =>ShyCycle5(),
                _ => throw new InvalidInstructionStepException(0x9C)
            },       
            // 0x9D STA abs, X
            0x9D => instructionTimer switch
            {
               Timing.T2 => StaAbsoluteXCycle1(),
               Timing.T3 =>StaAbsoluteXCycle2(),
               Timing.T4 => StaAbsoluteXCycle3(),
               Timing.T5 => StaAbsoluteXCycle4(),
               Timing.T6 =>StaAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x9D)
            },       
            // 0x9E SHX abs, Y
            0x9E => instructionTimer switch
            {
               Timing.T2 => ShxCycle1(),
               Timing.T3 =>ShxCycle2(),
               Timing.T4 => ShxCycle3(),
               Timing.T5 => ShxCycle4(),
               Timing.T6 =>ShxCycle5(),
                _ => throw new InvalidInstructionStepException(0x9E)
            },       
            // 0x9F SHX abs, Y
            0x9F => instructionTimer switch
            {
               Timing.T2 => ShaAbsoluteYCycle1(),
               Timing.T3 =>ShaAbsoluteYCycle2(),
               Timing.T4 => ShaAbsoluteYCycle3(),
               Timing.T5 => ShaAbsoluteYCycle4(),
               Timing.T6 =>ShaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x9F)
            },       
            // 0xA0 LDY imm
            0xA0 => instructionTimer switch
            {
               Timing.T2 => LdyImmCycle1(),
               Timing.T3 =>LdyImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xA0)
            },       
            // 0xA1 LDA X, ind
            0xA1 => instructionTimer switch
            {
               Timing.T2 => LdaIndirectXCycle1(),
               Timing.T3 =>LdaIndirectXCycle2(),
               Timing.T4 => LdaIndirectXCycle3(),
               Timing.T5 => LdaIndirectXCycle4(),
               Timing.T6 =>LdaIndirectXCycle5(),
               Timing.T0 => LdaIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xA1)
            },       
            // 0xA2 LDX imm
            0xA2 => instructionTimer switch
            {
               Timing.T2 => LdxImmCycle1(),
               Timing.T3 =>LdxImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xA2)
            },       
            // 0xA3 LAX X, ind
            0xA3 => instructionTimer switch
            {
               Timing.T2 => LaxIndirectXCycle1(),
               Timing.T3 =>LaxIndirectXCycle2(),
               Timing.T4 => LaxIndirectXCycle3(),
               Timing.T5 => LaxIndirectXCycle4(),
               Timing.T6 =>LaxIndirectXCycle5(),
               Timing.T0 => LaxIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xA3)
            },       
            // 0xA4 LDY zpg
            0xA4 => instructionTimer switch
            {
               Timing.T2 => LdyZeropageCycle1(),
               Timing.T3 =>LdyZeropageCycle2(),
               Timing.T4 => LdyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA4)
            },       
            // 0xA5 LDA zpg
            0xA5 => instructionTimer switch
            {
               Timing.T2 => LdaZeropageCycle1(),
               Timing.T3 =>LdaZeropageCycle2(),
               Timing.T4 => LdaZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA5)
            },       
            // 0xA6 LDX zpg
            0xA6 => instructionTimer switch
            {
               Timing.T2 => LdxZeropageCycle1(),
               Timing.T3 =>LdxZeropageCycle2(),
               Timing.T4 => LdxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA6)
            },       
            // 0xA7 LAX zpg
            0xA7 => instructionTimer switch
            {
               Timing.T2 => LaxZeropageCycle1(),
               Timing.T3 =>LaxZeropageCycle2(),
               Timing.T4 => LaxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA7)
            },       
            // 0xA8 TAY impl
            0xA8 => instructionTimer switch
            {
               TWO_CYCLE_FIRST_TIMING => LAST_CYCLE_TIMING,
               LAST_CYCLE_TIMING => Transfer(Accumulator, DestinationRegister.IndexY),
                _ => throw new InvalidInstructionStepException(0xA8)
            },       
            // 0xA9 LDA imm
            0xA9 => instructionTimer switch
            {
               TWO_CYCLE_FIRST_TIMING => LAST_CYCLE_TIMING,
               LAST_CYCLE_TIMING => Lda(),
                _ => throw new InvalidInstructionStepException(0xA9)
            },       
            // 0xAA TAX impl
            0xAA => instructionTimer switch
            {
               TWO_CYCLE_FIRST_TIMING => LAST_CYCLE_TIMING,
               LAST_CYCLE_TIMING => Transfer(Accumulator, DestinationRegister.IndexX),
                _ => throw new InvalidInstructionStepException(0xAA)
            },       
            // 0xAB LXA imm
            0xAB => instructionTimer switch
            {
               Timing.T2 => LxaCycle1(),
               Timing.T3 =>LxaCycle2(),
                _ => throw new InvalidInstructionStepException(0xAB)
            },       
            // 0xAC LDY abs
            0xAC => instructionTimer switch
            {
               Timing.T2 => LdyAbsoluteCycle1(),
               Timing.T3 =>LdyAbsoluteCycle2(),
               Timing.T4 => LdyAbsoluteCycle3(),
               Timing.T5 => LdyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAC)
            },       
            // 0xAD LDA abs
            0xAD => instructionTimer switch
            {
               Timing.T2 => LdaAbsoluteCycle1(),
               Timing.T3 =>LdaAbsoluteCycle2(),
               Timing.T4 => LdaAbsoluteCycle3(),
               Timing.T5 => LdaAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAD)
            },       
            // 0xAE LDX abs
            0xAE => instructionTimer switch
            {
               Timing.T2 => LdxAbsoluteCycle1(),
               Timing.T3 =>LdxAbsoluteCycle2(),
               Timing.T4 => LdxAbsoluteCycle3(),
               Timing.T5 => LdxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAE)
            },       
            // 0xAF LAX abs
            0xAF => instructionTimer switch
            {
               Timing.T2 => LaxAbsoluteCycle1(),
               Timing.T3 =>LaxAbsoluteCycle2(),
               Timing.T4 => LaxAbsoluteCycle3(),
               Timing.T5 => LaxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAF)
            },       
            // 0xB0 BCS rel
            0xB0 => instructionTimer switch
            {
               Timing.T2 => BcsCycle1(),
               Timing.T3 =>BcsCycle2(),
               Timing.T4 => BcsCycle3(),
               Timing.T5 => BcsCycle4(),
                _ => throw new InvalidInstructionStepException(0xB0)
            },       
            // 0xB1 LDA ind, Y
            0xB1 => instructionTimer switch
            {
               Timing.T2 => LdaIndirectYCycle1(),
               Timing.T3 =>LdaIndirectYCycle2(),
               Timing.T4 => LdaIndirectYCycle3(),
               Timing.T5 => LdaIndirectYCycle4(),
               Timing.T6 =>LdaIndirectYCycle5(),
               Timing.T0 => LdaIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xB1)
            },       
            // 0xB2 Jam
            0xB2 => Jam(),
            // 0xB3 LAX ind, Y
            0xB3 => instructionTimer switch
            {
               Timing.T2 => LaxIndirectYCycle1(),
               Timing.T3 =>LaxIndirectYCycle2(),
               Timing.T4 => LaxIndirectYCycle3(),
               Timing.T5 => LaxIndirectYCycle4(),
               Timing.T6 =>LaxIndirectYCycle5(),
               Timing.T0 => LaxIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xB3)
            },       
            // 0xB4 LDY zpg, X
            0xB4 => instructionTimer switch
            {
               Timing.T2 => LdyZeropageXCycle1(),
               Timing.T3 =>LdyZeropageXCycle2(),
               Timing.T4 => LdyZeropageXCycle3(),
               Timing.T5 => LdyZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xB4)
            },       
            // 0xB5 LDA zpg, X
            0xB5 => instructionTimer switch
            {
               Timing.T2 => LdaZeropageXCycle1(),
               Timing.T3 =>LdaZeropageXCycle2(),
               Timing.T4 => LdaZeropageXCycle3(),
               Timing.T5 => LdaZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xB5)
            },       
            // 0xB6 LDX zpg, Y
            0xB6 => instructionTimer switch
            {
               Timing.T2 => LdxZeropageYCycle1(),
               Timing.T3 =>LdxZeropageYCycle2(),
               Timing.T4 => LdxZeropageYCycle3(),
               Timing.T5 => LdxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0xB6)
            },       
            // 0xB7 LAX zpg, X
            0xB7 => instructionTimer switch
            {
               Timing.T2 => LaxZeropageYCycle1(),
               Timing.T3 =>LaxZeropageYCycle2(),
               Timing.T4 => LaxZeropageYCycle3(),
               Timing.T5 => LaxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0xB7)
            },       
            // 0xB8 CLV impl
            0xB8 => instructionTimer switch
            {
               Timing.T2 => ClvCycle1(),
               Timing.T3 =>ClvCycle2(),
                _ => throw new InvalidInstructionStepException(0xB8)
            },       
            // 0xB9 LDA abs, Y
            0xB9 => instructionTimer switch
            {
               Timing.T2 => LdaAbsoluteYCycle1(),
               Timing.T3 =>LdaAbsoluteYCycle2(),
               Timing.T4 => LdaAbsoluteYCycle3(),
               Timing.T5 => LdaAbsoluteYCycle4(),
               Timing.T6 =>LdaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xB9)
            },       
            // 0xBA TSX impl
            0xBA => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Transfer(StackPointer, DestinationRegister.IndexX),
                _ => throw new InvalidInstructionStepException(0xBA)
            },       
            // 0xBB LAS abs, Y
            0xBB => instructionTimer switch
            {
               Timing.T2 => LasCycle1(),
               Timing.T3 =>LasCycle2(),
               Timing.T4 => LasCycle3(),
               Timing.T5 => LasCycle4(),
                _ => throw new InvalidInstructionStepException(0xBB)
            },       
            // 0xBC LDY abs, X
            0xBC => instructionTimer switch
            {
               Timing.T2 => LdyAbsoluteXCycle1(),
               Timing.T3 =>LdyAbsoluteXCycle2(),
               Timing.T4 => LdyAbsoluteXCycle3(),
               Timing.T5 => LdyAbsoluteXCycle4(),
               Timing.T6 =>LdyAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xBC)
            },       
            // 0xBD LDA abs, X
            0xBD => instructionTimer switch
            {
               Timing.T2 => LdaAbsoluteXCycle1(),
               Timing.T3 =>LdaAbsoluteXCycle2(),
               Timing.T4 => LdaAbsoluteXCycle3(),
               Timing.T5 => LdaAbsoluteXCycle4(),
               Timing.T6 =>LdaAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xBD)
            },       
            // 0xBE LDX abs, Y
            0xBE => instructionTimer switch
            {
               Timing.T2 => LdxAbsoluteYCycle1(),
               Timing.T3 =>LdxAbsoluteYCycle2(),
               Timing.T4 => LdxAbsoluteYCycle3(),
               Timing.T5 => LdxAbsoluteYCycle4(),
               Timing.T6 =>LdxAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xBE)
            },       
            // 0xBF LAX abs, Y
            0xBF => instructionTimer switch
            {
               Timing.T2 => LaxAbsoluteYCycle1(),
               Timing.T3 =>LaxAbsoluteYCycle2(),
               Timing.T4 => LaxAbsoluteYCycle3(),
               Timing.T5 => LaxAbsoluteYCycle4(),
               Timing.T6 =>LaxAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xBF)
            },       
            // 0xC0 CPY imm
            0xC0 => instructionTimer switch
            {
               Timing.T2 => CpyImmCycle1(),
               Timing.T3 =>CpyImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xC0)
            },       
            // 0xC1 CMP X, ind
            0xC1 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectXCycle2(),
               Timing.T4 => IndirectXCycle3(),
               Timing.T5 =>  IndirectXCycle4(),
               Timing.T6 => IndirectXCycle5(false),
               Timing.T0 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xC1)
            },       
            // 0xC2 NOP
            0xC2 => Jam(),
            // 0xC3 DCP X, ind
            0xC3 => instructionTimer switch
            {
               Timing.T2 => DcpIndirectXCycle1(),
               Timing.T3 =>DcpIndirectXCycle2(),
               Timing.T4 => DcpIndirectXCycle3(),
               Timing.T5 => DcpIndirectXCycle4(),
               Timing.T6 =>DcpIndirectXCycle5(),
               Timing.T0 => DcpIndirectXCycle6(),
               Timing.TPlus => DcpIndirectXCycle7(),
               Timing.T8 => DcpIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0xC3)
            },       
            // 0xC4 CPY zpg
            0xC4 => instructionTimer switch
            {
               Timing.T2 => CpyZeropageCycle1(),
               Timing.T3 =>CpyZeropageCycle2(),
               Timing.T4 => CpyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xC4)
            },       
            // 0xC5 CMP zpg
            0xC5 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xC5)
            },       
            // 0xC6 DEC zpg
            0xC6 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 | Timing.SD1 => ZeropageRead(),
               Timing.T4 | Timing.SD2 => DecZeropageCycle3(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xC6)
            },       
            // 0xC7 DCP zpg
            0xC7 => instructionTimer switch
            {
               Timing.T2 => DcpZeropageCycle1(),
               Timing.T3 =>DcpZeropageCycle2(),
               Timing.T4 => DcpZeropageCycle3(),
               Timing.T5 => DcpZeropageCycle4(),
               Timing.T6 =>DcpZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0xC7)
            },       
            // 0xC8 INY impl
            0xC8 => instructionTimer switch
            {
               Timing.T2 => InyCycle1(),
               Timing.T3 =>InyCycle2(),
                _ => throw new InvalidInstructionStepException(0xC8)
            },       
            // 0xC9 CMP imm
            0xC9 => instructionTimer switch
            {
               Timing.T0 | Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xC9)
            },       
            // 0xCA DEX impl
            0xCA => instructionTimer switch
            {
               Timing.T2 => DexCycle1(),
               Timing.T3 =>DexCycle2(),
                _ => throw new InvalidInstructionStepException(0xCA)
            },       
            // 0xCB SBX imm
            0xCB => instructionTimer switch
            {
               Timing.T2 => SbxCycle1(),
               Timing.T3 =>SbxCycle2(),
                _ => throw new InvalidInstructionStepException(0xCB)
            },       
            // 0xCC CPY abs
            0xCC => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 => CpyAbsoluteCycle3(),
               Timing.T5 => CpyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xCC)
            },       
            // 0xCD CMP abs
            0xCD => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 => ReadDataPointer(),
               Timing.T5 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xCD)
            },       
            // 0xCE DEC abs
            0xCE => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => DecAbsoluteCycle4(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xCE)
            },       
            // 0xCF DCP abs
            0xCF => instructionTimer switch
            {
               Timing.T2 => DcpAbsoluteCycle1(),
               Timing.T3 =>DcpAbsoluteCycle2(),
               Timing.T4 => DcpAbsoluteCycle3(),
               Timing.T5 => DcpAbsoluteCycle4(),
               Timing.T6 =>DcpAbsoluteCycle5(),
               Timing.T0 => DcpAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xCF)
            },       
            // 0xD0 BNE rel
            0xD0 => instructionTimer switch
            {
               Timing.T2 => BneCycle1(),
               Timing.T3 =>BneCycle2(),
               Timing.T4 => BneCycle3(),
               Timing.T5 => BneCycle4(),
                _ => throw new InvalidInstructionStepException(0xD0)
            },       
            // 0xD1 CMP ind, Y
            0xD1 => instructionTimer switch
            {
                Timing.T2 => IndirectCycle1(),
                Timing.T3 => IndirectYCycle2(),
                Timing.T4 => IndirectYCycle3(),
                Timing.T5 => IndirectYCycle4(),
                Timing.T0 => IndirectYCycle5(),
                Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xD1)
            },       
            // 0xD2 Jam
            0xD2 => Jam(),
            // 0xD3 DCP ind, Y
            0xD3 => instructionTimer switch
            {
               Timing.T2 => DcpIndirectYCycle1(),
               Timing.T3 =>DcpIndirectYCycle2(),
               Timing.T4 => DcpIndirectYCycle3(),
               Timing.T5 => DcpIndirectYCycle4(),
               Timing.T6 =>DcpIndirectYCycle5(),
               Timing.T0 => DcpIndirectYCycle6(),
               Timing.TPlus => DcpIndirectYCycle7(),
               Timing.T8 => DcpIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0xD3)
            },       
            // 0xD4 NOP zpg, X
            0xD4 => instructionTimer switch
            {
               Timing.T2 => NopZeropageXCycle1(),
               Timing.T3 =>NopZeropageXCycle2(),
               Timing.T4 => NopZeropageXCycle3(),
               Timing.T5 => NopZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xD4)
            },       
            // 0xD5 CMP zpg, X
            0xD5 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 => ZeropageAddIndexXLow(),
               Timing.T0 => ZeropageRead(),
               Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xD5)
            },       
            // 0xD6 DEC zpg, X
            0xD6 => instructionTimer switch
            {
               Timing.T2 => DecZeropageXCycle1(),
               Timing.T3 =>DecZeropageXCycle2(),
               Timing.T4 => DecZeropageXCycle3(),
               Timing.T5 => DecZeropageXCycle4(),
               Timing.T6 =>DecZeropageXCycle5(),
               Timing.T0 => DecZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xD6)
            },       
            // 0xD7 DCP zpg, X
            0xD7 => instructionTimer switch
            {
               Timing.T2 => DcpZeropageXCycle1(),
               Timing.T3 =>DcpZeropageXCycle2(),
               Timing.T4 => DcpZeropageXCycle3(),
               Timing.T5 => DcpZeropageXCycle4(),
               Timing.T6 =>DcpZeropageXCycle5(),
               Timing.T0 => DcpZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xD7)
            },       
            // 0xD8 CLD impl
            0xD8 => instructionTimer switch
            {
               Timing.T2 => CldCycle1(),
               Timing.T3 =>CldCycle2(),
                _ => throw new InvalidInstructionStepException(0xD8)
            },       
            // 0xD9 CMP abs, Y
            0xD9 => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexYLow(),
               Timing.T4 => AbsoluteAddIndexYHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xD9)
            },       
            // 0xDA NOP impl
            0xDA => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(),
               Timing.T3 =>NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xDA)
            },       
            // 0xDB DCP abs, Y
            0xDB => instructionTimer switch
            {
               Timing.T2 => DcpAbsoluteYCycle1(),
               Timing.T3 =>DcpAbsoluteYCycle2(),
               Timing.T4 => DcpAbsoluteYCycle3(),
               Timing.T5 => DcpAbsoluteYCycle4(),
               Timing.T6 =>DcpAbsoluteYCycle5(),
               Timing.T0 => DcpAbsoluteYCycle6(),
               Timing.TPlus => DcpAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0xDB)
            },       
            // 0xDC NOP abs, X
            0xDC => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(),
               Timing.T3 =>NopAbsoluteXCycle2(),
               Timing.T4 => NopAbsoluteXCycle3(),
               Timing.T5 => NopAbsoluteXCycle4(),
               Timing.T6 =>NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xDC)
            },       
            // 0xDD CMP abs, X
            0xDD => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexXLow(),
               Timing.T4 => AbsoluteAddIndexXHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => Cmp(Accumulator),
                _ => throw new InvalidInstructionStepException(0xDD)
            },
            // 0xDE DEC abs, X
            0xDE => instructionTimer switch
            {
               Timing.T2 => DecAbsoluteXCycle1(),
               Timing.T3 =>DecAbsoluteXCycle2(),
               Timing.T4 => DecAbsoluteXCycle3(),
               Timing.T5 => DecAbsoluteXCycle4(),
               Timing.T6 =>DecAbsoluteXCycle5(),
               Timing.T0 => DecAbsoluteXCycle6(),
               Timing.TPlus => DecAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xDE)
            },
            // 0xDF DCP abs, X
            0xDF => instructionTimer switch
            {
               Timing.T2 => DcpAbsoluteXCycle1(),
               Timing.T3 =>DcpAbsoluteXCycle2(),
               Timing.T4 => DcpAbsoluteXCycle3(),
               Timing.T5 => DcpAbsoluteXCycle4(),
               Timing.T6 =>DcpAbsoluteXCycle5(),
               Timing.T0 => DcpAbsoluteXCycle6(),
               Timing.TPlus => DcpAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xDF)
            },
            // 0xE0 CPX imm
            0xE0 => instructionTimer switch
            {
               Timing.T2 => CpxImmCycle1(),
               Timing.T3 =>CpxImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xE0)
            },
            // 0xE1 SBC X, ind
            0xE1 => instructionTimer switch
            {
               Timing.T2 => SbcIndirectXCycle1(),
               Timing.T3 =>SbcIndirectXCycle2(),
               Timing.T4 => SbcIndirectXCycle3(),
               Timing.T5 => SbcIndirectXCycle4(),
               Timing.T6 =>SbcIndirectXCycle5(),
               Timing.T0 => SbcIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xE1)
            },
            // 0xE2 NOP imm
            0xE2 => instructionTimer switch
            {
               Timing.T0 |  Timing.T2 => Timing.TPlus | Timing.T1,
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xE2)
            },
            // 0xE3 ISC x, ind
            0xE3 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectXCycle2(),
               Timing.T4 => IndirectXCycle3(),
               Timing.T5 => IndirectXCycle4(),
               Timing.SD1 => IndirectXCycle5(true),
               Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xE3)
            },
            // 0xE4 CPX zpg
            0xE4 => instructionTimer switch
            {
               Timing.T2 => CpxZeropageCycle1(),
               Timing.T3 =>CpxZeropageCycle2(),
               Timing.T4 => CpxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xE4)
            },
            // 0xE5 SBC zpg
            0xE5 => instructionTimer switch
            {
               Timing.T2 => SbcZeropageCycle1(),
               Timing.T3 =>SbcZeropageCycle2(),
               Timing.T4 => SbcZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xE5)
            },
            // 0xE6 INC zpg
            0xE6 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 | Timing.SD1 => ZeropageRead(),
               Timing.T4 | Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xE6)
            },
            // 0xE7 ISC zpg
            0xE7 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 | Timing.SD1 => ReadDataPointer(),
               Timing.T4  | Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xE7)
            },
            // 0xE8 INX impl
            0xE8 => instructionTimer switch
            {
               Timing.T2 => InxCycle1(),
               Timing.T3 =>InxCycle2(),
                _ => throw new InvalidInstructionStepException(0xE8)
            },
            // 0xE9 SBC imm
            0xE9 => instructionTimer switch
            {
                Timing.T2 | Timing.T0 => Timing.T1 | Timing.TPlus,
                Timing.TPlus |  Timing.T1 => LogicSubtract(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0xE9)
            },
            // 0xEA NOP impl
            0xEA => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(),
               Timing.T3 =>NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xEA)
            },
            // 0xEB USBC imm
            0xEB => instructionTimer switch
            {
               Timing.T2 | Timing.T0 => Timing.T3,
               Timing.T3 => LogicSubtract(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0xEB)
            },
            // 0xEC CPX abs
            0xEC => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(Timing.T0),
               Timing.T0 => ReadDataPointer(Timing.T1 | Timing.TPlus),
               Timing.T1 | Timing.TPlus => Cmp(X),
                _ => throw new InvalidInstructionStepException(0xEC)
            },
            // 0xED SBC abs
            0xED => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(Timing.T0),
               Timing.T0 => ReadDataPointer(Timing.TPlus | Timing.T1),
               Timing.TPlus | Timing.T1 => LogicSubtract(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0xED)
            },
            // 0xEE INC abs
            0xEE => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => Timing.T2,
                _ => throw new InvalidInstructionStepException(0xEE)
            },
            // 0xEF ISC abs
            0xEF => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteCycle2(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xEF)
            },
            // 0xF0 BEQ rel
            0xF0 => instructionTimer switch
            {
               Timing.T2 => BeqCycle1(),
               Timing.T3 =>BeqCycle2(),
               Timing.T4 => BeqCycle3(),
               Timing.T5 => BeqCycle4(),
                _ => throw new InvalidInstructionStepException(0xF0)
            },
            // 0xF1 SBC ind, Y
            0xF1 => instructionTimer switch
            {
               Timing.T2 => SbcIndirectYCycle1(),
               Timing.T3 =>SbcIndirectYCycle2(),
               Timing.T4 => SbcIndirectYCycle3(),
               Timing.T5 => SbcIndirectYCycle4(),
               Timing.T6 =>SbcIndirectYCycle5(),
               Timing.T0 => SbcIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xF1)
            },
            // 0xF2 JAM
            0xF2 => Jam(),
            // 0xF3 ISC ind, Y
            0xF3 => instructionTimer switch
            {
               Timing.T2 => IndirectCycle1(),
               Timing.T3 => IndirectYCycle2(),
               Timing.T4 => IndirectYCycle3(),
               Timing.T5 => IndirectYCycle4(),
               Timing.SD1 => IndirectYCycle5(),
               Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xF3)
            },
            // 0xF4 NOP zpg, X
            0xF4 => instructionTimer switch
            {
               Timing.T2 => NopZeropageXCycle1(),
               Timing.T3 =>NopZeropageXCycle2(),
               Timing.T4 => NopZeropageXCycle3(),
               Timing.T5 => NopZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xF4)
            },
            // 0xF5 SBC zpg, X
            0xF5 => instructionTimer switch
            {
               Timing.T2 => SbcZeropageXCycle1(),
               Timing.T3 =>SbcZeropageXCycle2(),
               Timing.T4 => SbcZeropageXCycle3(),
               Timing.T5 => SbcZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xF5)
            },
            // 0xF6 INC zpg, X
            0xF6 => instructionTimer switch
            {
               Timing.T2 => IncZeropageXCycle1(),
               Timing.T3 =>IncZeropageXCycle2(),
               Timing.T4 => IncZeropageXCycle3(),
               Timing.T5 => IncZeropageXCycle4(),
               Timing.T6 =>IncZeropageXCycle5(),
               Timing.T0 => IncZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xF6)
            },
            // 0xF7 ISC zpg, X
            0xF7 => instructionTimer switch
            {
               Timing.T2 => ZeropageCycle1(),
               Timing.T3 => ZeropageAddIndexXLow(),
               Timing.T4 | Timing.SD1 => ReadDataPointer(),
               Timing.T5 | Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xF7)
            },
            // 0xF8 SED impl
            0xF8 => instructionTimer switch
            {
               Timing.T2 => SedCycle1(),
               Timing.T3 =>SedCycle2(),
                _ => throw new InvalidInstructionStepException(0xF8)
            },
            // 0xF9 SBC abs, Y
            0xF9 => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexYLow(Timing.T0, Timing.T4),
               Timing.T4 => AbsoluteAddIndexYHigh(),
               Timing.T0 => ReadDataPointer(),
               Timing.TPlus | Timing.T1 => LogicSubtract(MemoryDataRegister),
                _ => throw new InvalidInstructionStepException(0xF9)
            },
            // 0xFA NOP impl
            0xFA => instructionTimer switch
            {
               Timing.T2 => NopImpliedCycle1(),
               Timing.T3 =>NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xFA)
            },
            // 0xFB ISC abs, Y
            0xFB => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexYLow(),
               Timing.T4 => AbsoluteAddIndexYHigh(),
               Timing.T5 | Timing.SD1 => ReadDataPointer(),
               Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xFB)
            },
            // 0xFC NOP abs, X
            0xFC => instructionTimer switch
            {
               Timing.T2 => NopAbsoluteXCycle1(),
               Timing.T3 =>NopAbsoluteXCycle2(),
               Timing.T4 => NopAbsoluteXCycle3(),
               Timing.T5 => NopAbsoluteXCycle4(),
               Timing.T6 =>NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xFC)
            },
            // 0xFD SBC abs, X
            0xFD => instructionTimer switch
            {
               Timing.T2 => SbcAbsoluteXCycle1(),
               Timing.T3 =>SbcAbsoluteXCycle2(),
               Timing.T4 => SbcAbsoluteXCycle3(),
               Timing.T5 => SbcAbsoluteXCycle4(),
               Timing.T6 =>SbcAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xFD)
            },
            // 0xFE INC abs, X
            0xFE => instructionTimer switch
            {
               Timing.T2 => IncAbsoluteXCycle1(),
               Timing.T3 =>IncAbsoluteXCycle2(),
               Timing.T4 => IncAbsoluteXCycle3(),
               Timing.T5 => IncAbsoluteXCycle4(),
               Timing.T6 =>IncAbsoluteXCycle5(),
               Timing.T0 => IncAbsoluteXCycle6(),
               Timing.TPlus => IncAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xFE)
            },
            // 0xFF ISC abs, X
            // ReSharper disable once PatternIsRedundant
            0xFF => instructionTimer switch
            {
               Timing.T2 => AbsoluteCycle1(),
               Timing.T3 => AbsoluteAddIndexXLow(),
               Timing.T4 => AbsoluteAddIndexXHigh(),
               Timing.T5 | Timing.SD1 => ReadDataPointer(),
               Timing.SD2 => Inc(),
               Timing.T0 => Write(MemoryAddressRegister, TempRegister),
               Timing.TPlus | Timing.T1 => LogicSubtract(TempRegister),
                _ => throw new InvalidInstructionStepException(0xFF)
            },
        };
    
    // Functions that a lot of instructions have in common.
    
    
    private Timing ReadDataPointer(Timing resultTiming = Timing.T5 | Timing.SD2)
    {
        MemoryAddressRegister = DataPointer;
        return resultTiming;
    }
    
    private Timing Write(ushort address, byte @value)
    {
        MemoryAddressRegister = address;
        ReadPin = false;
        MemoryDataOutputRegister = @value;
        return LAST_CYCLE_TIMING;
    }

    private Timing AbsoluteCycle1()
    {
        MemoryDataDestination = MemoryDataDestination.DataPointerLow;
        return Timing.T3;
    }

    private Timing AbsoluteCycle2(Timing resultTiming = Timing.T4 | Timing.SD1)
    {
        MemoryDataDestination = MemoryDataDestination.DataPointerHigh;
        ProgramCounter++;
        return resultTiming;
    }

    private Timing AbsoluteAddIndexLow(byte registerValue, Timing resultTiming = Timing.T5 | Timing.SD1,
        Timing resultTimingPageCross = Timing.SD2)
    {
        AbsoluteCycle2(); // Load the next byte simultaneously.
        TempRegister = DataPointerLow;
        int result = DataPointerLow + registerValue;
        DataPointerLow = (byte)result;
        return result > 0xFF ? resultTimingPageCross : resultTiming;
    }
    
    private Timing AbsoluteAddIndexHigh(byte registerValue, Timing resultTiming = Timing.T5 | Timing.SD1)
    {
        int result = TempRegister + registerValue;
        DataPointerHigh = (byte)(result >> 8);
        return resultTiming;
    }
    
    private Timing AbsoluteAddIndexYLow(Timing resultTiming = Timing.T5 | Timing.SD1,
        Timing resultTimingPageCross = Timing.SD2)
        => AbsoluteAddIndexLow(Y, resultTiming, resultTimingPageCross);
    
    private Timing AbsoluteAddIndexYHigh(Timing resultTiming = Timing.T0)
        => AbsoluteAddIndexHigh(Y, resultTiming);
    
    private Timing AbsoluteAddIndexXLow(Timing resultTiming = Timing.T5 | Timing.SD1,
        Timing resultTimingPageCross = Timing.SD2)
        => AbsoluteAddIndexLow(X, resultTiming, resultTimingPageCross);
    
    private Timing AbsoluteAddIndexXHigh(Timing resultTiming = Timing.T0)
        => AbsoluteAddIndexHigh(X, resultTiming);

    private Timing IndirectCycle1() => Timing.T3;

    private Timing IndirectXCycle2()
    {
        TempRegister = (byte)(X + MemoryDataRegister);
        return Timing.T4;
    }
    
    private Timing IndirectXCycle3()
    {
        MemoryAddressRegister = TempRegister;
        MemoryDataDestination = MemoryDataDestination.DataPointerLow;
        return Timing.T5;
    }
    
    private Timing IndirectXCycle4()
    {
        MemoryAddressRegister++;
        MemoryDataDestination = MemoryDataDestination.DataPointerHigh;
        return Timing.T0;
    }

    private Timing IndirectXCycle5(bool illegalRmw)
    {
        ReadDataPointer(Timing.T0);
        return illegalRmw ? Timing.SD1 : Timing.TPlus | Timing.T1;
    } 
    
    private Timing IndirectYCycle2()
    {
        MemoryAddressRegister = MemoryDataRegister;
        MemoryDataDestination = MemoryDataDestination.DataPointerLow;
        return Timing.T4;
    }
    
    private Timing IndirectYCycle3()
    {
        MemoryAddressRegister++;
        MemoryDataDestination = MemoryDataDestination.DataPointerHigh;

        TempRegister = DataPointerLow;
        int result = DataPointerLow + Y + (CarryBit ? 1 : 0);
        DataPointerLow = (byte)result;
        return (result > 0xFF) ? Timing.T5 : Timing.T0;
    }
    
    private Timing IndirectYCycle4()
    {
        DataPointerHigh = (byte)((TempRegister + Y) >> 8);
        return Timing.T0;
    }

    private Timing IndirectYCycle5()
    {
        MemoryAddressRegister = DataPointerHigh;
        return Timing.TPlus | Timing.T1;
    } 

    private Timing ZeropageCycle1(Timing resultTiming = Timing.T3 | Timing.SD1)
    {
        MemoryDataDestination = MemoryDataDestination.DataPointerLow;
        MemoryAddressHigh  = 0; // Set high nibble to 0.
        return resultTiming;
    }
    
    private Timing ZeropageRead(Timing resultTiming = Timing.T4 | Timing.SD2)
    {
        MemoryAddressRegister = DataPointer;
        return resultTiming;
    }

    private Timing ZeropageAddIndexXLow()
    {
        DataPointerLow += X;
        return Timing.T4;
    }

    private Timing RmwDummyWrite()
    {
        ReadPin = false;
        MemoryDataOutputRegister = MemoryDataRegister;
        return Timing.T0;
    }

    
    /// <summary>
    /// Last cycle of ALR. AND the Accumulator with the contents of MDR and then perform a right shift.
    /// Store the result in the Accumulator and the status register.
    /// Micro ops:
    /// A ← (A AND MDR) LSR 1
    /// N ← 0
    /// Z ← 1 if A is 0, otherwise 1
    /// C ← 1 if A was odd, otherwise 0
    /// </summary>
    /// <returns><see cref="Timing.T2"/>, this is the last cycle.</returns>
    private Timing Alr()
    {
        var (result, negative, zero, carry) = AluLogicalShiftRight(Accumulator & MemoryDataRegister);
        Accumulator = result;
        NegativeBit = negative;
        ZeroBit = zero;
        CarryBit = carry;
        return Timing.T2;
    }

    // sbx
    private Timing SbxCycle2()
    {
        return Timing.T1;
    }

    private Timing SbxCycle1()
    {
        return Timing.T1;
    }

    // dcp
    private Timing DcpZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageCycle1()
    {
        return Timing.T1;
    }


    private Timing DcpZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing DcpAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing DcpAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing DcpAbsoluteYCycle7()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing DcpIndirectXCycle8()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle7()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing DcpIndirectYCycle8()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle7()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing DcpIndirectYCycle1()
    {
        return Timing.T1;
    }

    // las

    private Timing LasCycle4()
    {
        return Timing.T1;
    }
    private Timing LasCycle3()
    {
        return Timing.T1;
    }
    private Timing LasCycle2()
    {
        return Timing.T1;
    }
    private Timing LasCycle1()
    {
        return Timing.T1;
    }

    // lxa
    private Timing LxaCycle2()
    {
        return Timing.T1;
    }
    private Timing LxaCycle1()
    {
        return Timing.T1;
    }

    // lax

    private Timing LaxZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing LaxZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing LaxZeropageCycle1()
    {
        return Timing.T1;
    }


    private Timing LaxZeropageYCycle4()
    {
        return Timing.T1;
    }
    private Timing LaxZeropageYCycle3()
    {
        return Timing.T1;
    }
    private Timing LaxZeropageYCycle2()
    {
        return Timing.T1;
    }
    private Timing LaxZeropageYCycle1()
    {
        return Timing.T1;
    }

    private Timing LaxAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing LaxAbsoluteCycle3()
    {
        return Timing.T1;
    }

    private Timing LaxAbsoluteCycle2()
    {
        return Timing.T1;
    }

    private Timing LaxAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing LaxAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing LaxAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing LaxAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing LaxAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing LaxAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing LaxIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing LaxIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing LaxIndirectYCycle1()
    {
        return Timing.T1;
    }

    // shx
    private Timing ShxCycle5()
    {
        return Timing.T1;
    }
    private Timing ShxCycle4()
    {
        return Timing.T1;
    }
    private Timing ShxCycle3()
    {
        return Timing.T1;
    }
    private Timing ShxCycle2()
    {
        return Timing.T1;
    }
    private Timing ShxCycle1()
    {
        return Timing.T1;
    }

    // shy
    private Timing ShyCycle5()
    {
        return Timing.T1;
    }
    private Timing ShyCycle4()
    {
        return Timing.T1;
    }
    private Timing ShyCycle3()
    {
        return Timing.T1;
    }
    private Timing ShyCycle2()
    {
        return Timing.T1;
    }
    private Timing ShyCycle1()
    {
        return Timing.T1;
    }

    // tas
    private Timing TasCycle5()
    {
        return Timing.T1;
    }
    private Timing TasCycle4()
    {
        return Timing.T1;
    }
    private Timing TasCycle3()
    {
        return Timing.T1;
    }
    private Timing TasCycle2()
    {
        return Timing.T1;
    }
    private Timing TasCycle1()
    {
        return Timing.T1;
    }

    // SHA
    private Timing ShaIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing ShaIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing ShaIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing ShaIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing ShaIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing ShaAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing ShaAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing ShaAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing ShaAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing ShaAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    // JAM (illegal opcode).
    private Timing Jam() => InstructionTimer switch
    {
        Timing.T2 => Timing.T3,
        Timing.T3 => Timing.T4,
        Timing.T4 => Timing.T5,
        _ => Timing.Nothing,
    };

    private Timing AneCycle2()
    {
        Accumulator = (byte)((Accumulator | 0xee | X) & MemoryDataRegister);
        NegativeBit = (Accumulator & 0x80) == 0x80;
        ZeroBit = Accumulator == 0;
        return Timing.T2;
    }

    // NOPs (illegal opcodes).
    private Timing NopImpliedCycle2()
    {
        return Timing.T1;
    }
    private Timing NopImpliedCycle1()
    {
        return Timing.T1;
    }



    private Timing NopZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing NopZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing NopZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing NopZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing NopZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing NopZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing NopZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing NopAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing NopAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing NopAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing NopAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing NopAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing AncCycle3()
    {
        return Timing.T1;
    }
    private Timing AncCycle2()
    {
        return Timing.T1;
    }
    private Timing AncCycle1()
    {
        return Timing.T1;
    }

    private Timing ArrCycle2()
    {
        return Timing.T1;
    }
    private Timing ArrCycle1()
    {
        return Timing.T1;
    }

    // Proper CPU instruction steps.
    private Timing ClcCycle2()
    {
        return Timing.T1;
    }
    private Timing ClcCycle1()
    {
        return Timing.T1;
    }

    private Timing SeiCycle2()
    {
        return Timing.T1;
    }
    private Timing SeiCycle1()
    {
        return Timing.T1;
    }

    private Timing PlaCycle4()
    {
        return Timing.T1;
    }
    private Timing PlaCycle3()
    {
        return Timing.T1;
    }
    private Timing PlaCycle2()
    {
        return Timing.T1;
    }
    private Timing PlaCycle1()
    {
        return Timing.T1;
    }


    private Timing SecCycle2()
    {
        CarryBit = true;
        return Timing.T2;
    }
    private Timing SecCycle1() => Timing.TPlus | Timing.T1;

    private Timing PlpCycle4()
    {
        return Timing.T1;
    }
    private Timing PlpCycle3()
    {
        return Timing.T1;
    }
    private Timing PlpCycle2()
    {
        return Timing.T1;
    }
    private Timing PlpCycle1()
    {
        return Timing.T1;
    }

    private Timing PhaCycle3()
    {
        return Timing.T1;
    }
    private Timing PhaCycle2()
    {
        return Timing.T1;
    }
    private Timing PhaCycle1()
    {
        return Timing.T1;
    }

    private Timing DexCycle2()
    {
        return Timing.T1;
    }
    private Timing DexCycle1()
    {
        return Timing.T1;
    }

    private Timing DeyCycle2()
    {
        var (result, negative, zero, _, _) = AluAdd(Y, -1, false);
        Y = result;
        NegativeBit = negative;
        ZeroBit = zero;
        return Timing.T2;
    }

    private Timing InyCycle2()
    {
        return Timing.T1;
    }
    private Timing InyCycle1()
    {
        return Timing.T1;
    }

    private Timing CldCycle2()
    {
        return Timing.T1;
    }
    private Timing CldCycle1()
    {
        return Timing.T1;
    }

    private Timing InxCycle2()
    {
        return Timing.T1;
    }
    private Timing InxCycle1()
    {
        return Timing.T1;
    }

    private Timing SedCycle2()
    {
        return Timing.T1;
    }
    private Timing SedCycle1()
    {
        return Timing.T1;
    }

    private Timing Transfer(byte @value, DestinationRegister destinationRegister)
    {
        var (result, zero, negative, _, __) = AluAdd(@value, 0, false);
        switch (destinationRegister)
        {
            case DestinationRegister.IndexX:
                X = result;
                NegativeBit = negative;
                ZeroBit = zero;
                break;
            case DestinationRegister.IndexY:
                Y = result;
                NegativeBit = negative;
                ZeroBit = zero;
                break;
            case DestinationRegister.Accumulator:
                Accumulator = result;
                NegativeBit = negative;
                ZeroBit = zero;
                break;
            case DestinationRegister.StackPointer:
                StackPointer = result;
                break;
        }
        return Timing.T2;
    }
    
    private Timing ClvCycle2()
    {
        return Timing.T1;
    }
    private Timing ClvCycle1()
    {
        return Timing.T1;
    }

    private Timing CliCycle2()
    {
        return Timing.T1;
    }
    private Timing CliCycle1()
    {
        return Timing.T1;
    }

    private Timing PhpCycle3()
    {
        return Timing.T1;
    }
    private Timing PhpCycle2()
    {
        return Timing.T1;
    }
    private Timing PhpCycle1()
    {
        return Timing.T1;
    }

    private Timing BrkCycle7()
    {
        return Timing.T2;
    }
    private Timing BrkCycle6()
    {
        return Timing.TPlus | Timing.T1;
    }
    private Timing BrkCycle5()
    {
        return Timing.T0;
    }
    private Timing BrkCycle4()
    {
        return Timing.T6;
    }
    private Timing BrkCycle3()
    {
        return Timing.T5 | Timing.V0;
    }
    private Timing BrkCycle2()
    {
        return Timing.T4;
    }
    private Timing BrkCycle1()
    {
        return Timing.T3;
    }

    private Timing BplCycle4()
    {
        return Timing.T1;
    }
    private Timing BplCycle3()
    {
        return Timing.T1;
    }
    private Timing BplCycle2()
    {
        return Timing.T1;
    }
    private Timing BplCycle1()
    {
        return Timing.T1;
    }

    private Timing JsrCycle6()
    {
        return Timing.T1;
    }
    private Timing JsrCycle5()
    {
        return Timing.T1;
    }
    private Timing JsrCycle4()
    {
        return Timing.T1;
    }
    private Timing JsrCycle3()
    {
        return Timing.T1;
    }
    private Timing JsrCycle2()
    {
        return Timing.T1;
    }
    private Timing JsrCycle1()
    {
        return Timing.T1;
    }

    private Timing BmiCycle4()
    {
        return Timing.T1;
    }
    private Timing BmiCycle3()
    {
        return Timing.T1;
    }
    private Timing BmiCycle2()
    {
        return Timing.T1;
    }
    private Timing BmiCycle1()
    {
        return Timing.T1;
    }

    private Timing RtiCycle6()
    {
        return Timing.T1;
    }
    private Timing RtiCycle5()
    {
        return Timing.T1;
    }
    private Timing RtiCycle4()
    {
        return Timing.T1;
    }
    private Timing RtiCycle3()
    {
        return Timing.T1;
    }
    private Timing RtiCycle2()
    {
        return Timing.T1;
    }
    private Timing RtiCycle1()
    {
        return Timing.T1;
    }

    private Timing BvcCycle4()
    {
        return Timing.T1;
    }
    private Timing BvcCycle3()
    {
        return Timing.T1;
    }
    private Timing BvcCycle2()
    {
        return Timing.T1;
    }
    private Timing BvcCycle1()
    {
        return Timing.T1;
    }

    private Timing BvsCycle4()
    {
        return Timing.T1;
    }
    private Timing BvsCycle3()
    {
        return Timing.T1;
    }
    private Timing BvsCycle2()
    {
        return Timing.T1;
    }
    private Timing BvsCycle1()
    {
        return Timing.T1;
    }


    private Timing RtsCycle6()
    {
        return Timing.T1;
    }
    private Timing RtsCycle5()
    {
        return Timing.T1;
    }
    private Timing RtsCycle4()
    {
        return Timing.T1;
    }
    private Timing RtsCycle3()
    {
        return Timing.T1;
    }
    private Timing RtsCycle2()
    {
        return Timing.T1;
    }
    private Timing RtsCycle1()
    {
        return Timing.T1;
    }

    private Timing BccCycle4()
    {
        return Timing.T1;
    }
    private Timing BccCycle3()
    {
        return Timing.T1;
    }
    private Timing BccCycle2()
    {
        return Timing.T1;
    }
    private Timing BccCycle1()
    {
        return Timing.T1;
    }

    private Timing LdyImmCycle2()
    {
        return Timing.T1;
    }
    private Timing LdyImmCycle1()
    {
        return Timing.T1;
    }

    private Timing LdyZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing LdyZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing LdyZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing LdyZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing LdyZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing LdyZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing LdyZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing LdyAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing LdyAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing LdyAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing BcsCycle4()
    {
        return Timing.T1;
    }
    private Timing BcsCycle3()
    {
        return Timing.T1;
    }
    private Timing BcsCycle2()
    {
        return Timing.T1;
    }
    private Timing BcsCycle1()
    {
        return Timing.T1;
    }

    private Timing CpyImmCycle2()
    {
        return Timing.T1;
    }
    private Timing CpyImmCycle1()
    {
        return Timing.T1;
    }

    private Timing CpyZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing CpyZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing CpyZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing CpyAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing CpyAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing CpyAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing CpyAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing BneCycle4()
    {
        return Timing.T1;
    }
    private Timing BneCycle3()
    {
        return Timing.T1;
    }
    private Timing BneCycle2()
    {
        return Timing.T1;
    }
    private Timing BneCycle1()
    {
        return Timing.T1;
    }

    private Timing CpxImmCycle2()
    {
        return Timing.T1;
    }
    private Timing CpxImmCycle1()
    {
        return Timing.T1;
    }

    private Timing CpxZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing CpxZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing CpxZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing BeqCycle4()
    {
        return Timing.T1;
    }
    private Timing BeqCycle3()
    {
        return Timing.T1;
    }
    private Timing BeqCycle2()
    {
        return Timing.T1;
    }
    private Timing BeqCycle1()
    {
        return Timing.T1;
    }

    private Timing OraZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing OraZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing OraZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing OraZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing AndZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing AndZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing AndZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing AndZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing EorZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing EorZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing EorZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing EorZeropageXCycle1()
    {
        return Timing.T1;
    }


    private Timing EorAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing EorAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing EorAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing EorAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing EorAbsoluteYCycle1()
    {
        return Timing.T1;
    }


    private Timing EorIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing EorIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing EorIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing EorIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing EorIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing EorIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing StaZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing StaZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing StaZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing StaZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing StaZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing StaZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing StaZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing StaAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing StaAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing StaAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing StaAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing StaIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing StaIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing StaIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing StaIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing StaIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing StaIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing StaIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing StaIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing StaIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing StaIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing StaIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing StaIndirectYCycle1()
    {
        return Timing.T1;
    }

    /// <summary>
    ///  Set A to the data latch. Set Z and N according the values in A.
    /// A ← MDR
    /// Z ← A = 0
    /// N ← A gt 0
    /// </summary>
    /// <returns>T2. The instruction is complete.</returns>
    private Timing Lda()
    {
        Accumulator = MemoryDataRegister;
        NegativeBit = Accumulator > 0;
        ZeroBit = Accumulator == 0;
        return Timing.T2;
    }

    private Timing LdaZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing LdaIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing LdaIndirectYCycle1()
    {
        return Timing.T1;
    }



    private Timing CmpImmCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpImmCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing CmpIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing CmpIndirectYCycle1()
    {
        return Timing.T1;
    }


    private Timing SbcZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing SbcZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing SbcZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing SbcZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing SbcZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing SbcZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing SbcZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing SbcAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing SbcAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing SbcAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing SbcAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing SbcAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing SbcIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing SbcIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing SbcIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing LdxImmCycle2()
    {
        return Timing.T1;
    }
    private Timing LdxImmCycle1()
    {
        return Timing.T1;
    }

    private Timing LdxZeropageYCycle1()
    {
        return Timing.T1;
    }
    
    private Timing LdxZeropageYCycle2()
    {
        return Timing.T1;
    }
    
    private Timing LdxZeropageYCycle3()
    {
        return Timing.T1;
    }
    
    private Timing LdxZeropageYCycle4()
    {
        return Timing.T1;
    }

    private Timing LdxZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing LdxZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing LdxZeropageCycle1()
    {
        return Timing.T1;
    }


    private Timing LdxAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing LdxAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing LdxAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing BitZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing BitZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing StyZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing StyZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing StyZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing StyZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing StyZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing StyZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing StyZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing StyAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing StyAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing StyAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing StyAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing AdcZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing AdcIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing AdcIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing AslAccumCycle2()
    {
        return Timing.T1;
    }
    private Timing AslAccumCycle1()
    {
        return Timing.T1;
    }

    private Timing AslZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing AslZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing AslZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing AslZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing AslZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing AslZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing AslZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing AslZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing AslZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing AslZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing AslZeropageXCycle1()
    {
        return Timing.T1;
    }


    private Timing Asl()
    {
        RmwDummyWrite();
        var (result, carry, zero, overflow) = AluAsl(MemoryDataRegister);
        TempRegister = result;
        CarryBit = carry;
        ZeroBit = zero;
        OverflowBit = overflow;
        return Timing.T0;
    }

    private Timing AslAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing AslAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing RolZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing RolZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing RolZeropageXCycle1()
    {
        return Timing.T1;
    }

     private Timing Rol(bool accumulatorMode = false)
    {
        var (result, negative, zero, carry) = AluRotateLeft(MemoryDataRegister);
        if (accumulatorMode)
        {
            Accumulator = result;
        }
        else
        {
            TempRegister = result;
        }
        NegativeBit = negative;
        ZeroBit = zero;
        CarryBit = carry;
        return accumulatorMode ? Timing.T2 : Timing.T0;
    }

    private Timing RorAccumCycle2()
    {
        return Timing.T1;
    }
    private Timing RorAccumCycle1()
    {
        return Timing.T1;
    }

    private Timing RorZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing RorZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing RorZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing RorZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing RorZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing RorZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing RorZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing RorZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing RorZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing RorZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing RorZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing RorAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing RorAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing RorAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing Slo()
    {
        RmwDummyWrite();
        var (result, _, __, ___) = AluAsl(MemoryDataRegister);
        TempRegister = result;
        var (____, zero, negative) = AluOr(Accumulator, result);
        ZeroBit = zero;
        NegativeBit = negative;
        return Timing.TPlus | Timing.T1;
    }

    private Timing SloAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing SloAbsoluteYCycle7()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle6()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing SloAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing SloZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing SloZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing SloZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing SloZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing SloZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing SloIndirectXCycle8()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle7()
    {
        return Timing.T1;
    }

    private Timing SloIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing SloIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing SloIndirectYCycle8()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle7()
    {
        return Timing.T1;
    }

    private Timing SloIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing SloIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaAbsoluteYCycle7()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaIndirectXCycle8()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle7()
    {
        return Timing.T1;
    }

    private Timing RlaIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing RlaIndirectYCycle8()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle7()
    {
        return Timing.T1;
    }

    private Timing RlaIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing RlaIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing SreAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing SreAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing SreAbsoluteYCycle7()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle6()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing SreAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing SreZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing SreZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing SreZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing SreZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing SreZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing SreZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing SreZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing SreZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing SreZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing SreZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing SreZeropageXCycle1()
    {
        return Timing.T1;
    }


    private Timing SreIndirectXCycle8()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle7()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing SreIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing SreIndirectYCycle8()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle7()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing SreIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing LsrAccumCycle2()
    {
        return Timing.T1;
    }
    private Timing LsrAccumCycle1()
    {
        return Timing.T1;
    }

    private Timing LsrZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing LsrZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing LsrZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing LsrAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing LsrAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing LsrAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing RraAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing RraAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing RraAbsoluteYCycle7()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle6()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle5()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle4()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle3()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle2()
    {
        return Timing.T1;
    }
    private Timing RraAbsoluteYCycle1()
    {
        return Timing.T1;
    }

    private Timing RraZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing RraZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing RraZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing RraZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing RraZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing RraZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing RraZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing RraZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing RraZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing RraZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing RraZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing RraIndirectXCycle8()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle7()
    {
        return Timing.T1;
    }

    private Timing RraIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing RraIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing RraIndirectYCycle8()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle7()
    {
        return Timing.T1;
    }

    private Timing RraIndirectYCycle6()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle5()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle4()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle3()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle2()
    {
        return Timing.T1;
    }
    private Timing RraIndirectYCycle1()
    {
        return Timing.T1;
    }

    private Timing JmpAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing JmpAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing JmpAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing JmpAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing JmpIndirectCycle5()
    {
        return Timing.T1;
    }
    private Timing JmpIndirectCycle4()
    {
        return Timing.T1;
    }
    private Timing JmpIndirectCycle3()
    {
        return Timing.T1;
    }
    private Timing JmpIndirectCycle2()
    {
        return Timing.T1;
    }
    private Timing JmpIndirectCycle1()
    {
        return Timing.T1;
    }

    private Timing SaxAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing SaxAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing SaxAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing SaxAbsoluteCycle1()
    {
        return Timing.T1;
    }


    private Timing SaxZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing SaxZeropageYCycle6()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageYCycle5()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageYCycle4()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageYCycle3()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageYCycle2()
    {
        return Timing.T1;
    }
    private Timing SaxZeropageYCycle1()
    {
        return Timing.T1;
    }

    private Timing SaxIndirectXCycle6()
    {
        return Timing.T1;
    }
    private Timing SaxIndirectXCycle5()
    {
        return Timing.T1;
    }
    private Timing SaxIndirectXCycle4()
    {
        return Timing.T1;
    }
    private Timing SaxIndirectXCycle3()
    {
        return Timing.T1;
    }
    private Timing SaxIndirectXCycle2()
    {
        return Timing.T1;
    }
    private Timing SaxIndirectXCycle1()
    {
        return Timing.T1;
    }

    private Timing StxZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing StxZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing StxZeropageCycle1()
    {
        return Timing.T1;
    }

    private Timing StxZeropageYCycle4()
    {
        return Timing.T1;
    }
    private Timing StxZeropageYCycle3()
    {
        return Timing.T1;
    }
    private Timing StxZeropageYCycle2()
    {
        return Timing.T1;
    }
    private Timing StxZeropageYCycle1()
    {
        return Timing.T1;
    }

    private Timing StxAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing StxAbsoluteCycle3()
    {
        return Timing.T1;
    }
    private Timing StxAbsoluteCycle2()
    {
        return Timing.T1;
    }
    private Timing StxAbsoluteCycle1()
    {
        return Timing.T1;
    }

    private Timing DecZeropageCycle3()
    {
        return Timing.T1;
    }

    private Timing DecZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing DecZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing DecZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing DecZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing DecZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing DecZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing DecAbsoluteCycle4()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing DecAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    // INC
    private Timing IncZeropageCycle5()
    {
        return Timing.T1;
    }
    private Timing IncZeropageCycle4()
    {
        return Timing.T1;
    }
    private Timing IncZeropageCycle3()
    {
        return Timing.T1;
    }
    private Timing IncZeropageCycle2()
    {
        return Timing.T1;
    }
    private Timing IncZeropageCycle1()
    {
        return Timing.T1;
    }


    private Timing IncZeropageXCycle6()
    {
        return Timing.T1;
    }
    private Timing IncZeropageXCycle5()
    {
        return Timing.T1;
    }
    private Timing IncZeropageXCycle4()
    {
        return Timing.T1;
    }
    private Timing IncZeropageXCycle3()
    {
        return Timing.T1;
    }
    private Timing IncZeropageXCycle2()
    {
        return Timing.T1;
    }
    private Timing IncZeropageXCycle1()
    {
        return Timing.T1;
    }

    private Timing IncAbsoluteCycle6()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteCycle5()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteCycle4()
    {
        return Timing.T1;
    }

    private Timing IncAbsoluteXCycle7()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle6()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle5()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle4()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle3()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle2()
    {
        return Timing.T1;
    }
    private Timing IncAbsoluteXCycle1()
    {
        return Timing.T1;
    }

    private Timing Bit()
    {
        NegativeBit = (MemoryDataRegister & 0x80) == 0x80;
        OverflowBit = (MemoryDataRegister & 0x40) == 0x40;
        ZeroBit = (MemoryDataRegister & Accumulator) == 0;
        return Timing.T2;
    }

    private Timing Inc()
    {
        RmwDummyWrite();
        TempRegister = (byte)(MemoryDataRegister + 1);
        return Timing.T0;
    }

    private Timing Cmp(byte registerValue)
    {
        LogicCompare(registerValue, MemoryDataRegister);
        return Timing.T2;
    }
}