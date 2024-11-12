using System;
using TogetherNess.Utils;

namespace TogetherNess.Hardware;

public class Mos6502
{
    private int CurrentInstructionStep { get; set; } = 0;
    
    /// <summary>
    /// Effective Address.
    /// </summary>
    public ushort EffectiveAddress { get; set; } = 0;
    
    /// <summary>
    /// Accumulator.
    /// </summary>
    public byte Accumulator { get; set; } = 0;

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
    /// Program Counter.
    /// </summary>
    public byte ProgramCounter { get; set; } = 0;

    /// <summary>
    /// Status register (only 6 bits are used by the ALU).
    /// </summary>
    public byte Status { get; set; } = 0;

    /// <summary>
    /// Input data latch from memory.
    /// </summary>
    public byte DataLatch { get; set; } = 0;

    /// <summary>
    /// True if the CPU is reading.
    /// </summary>
    public bool ReadPin { get; set; } = false;
    
    private byte CurrentInstruction { get; set; } = 0;

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

    public Mos6502()
    {
    }
    
    public byte CpuAdd(byte a, byte b)
    {
        // Signed add.
        try
        {
            checked
            {
                var _ = ((sbyte)a) + ((sbyte)b);
            }
            OverflowBit = false;
        }
        catch (OverflowException)
        {
            OverflowBit = true;
        }
        // Unsigned add
        byte result = 0;
        try
        {
            checked
            {
                result = (byte)(a + b);
            }
            CarryBit = false;
        }
        catch (OverflowException)
        {
            CarryBit = true;
        }
        return result;
    }

    public void Tick()
    {
        if (CurrentInstructionStep != 0)
        {
            // In the middle of an instruction. Perform read and do the next step.
            CurrentInstructionStep = CpuTick();
            if (CurrentInstructionStep == 0)
            {
                CurrentInstruction++;
            }

            return;
        }
        
        // No current instruction. Decode the next instruction.
        EffectiveAddress = ProgramCounter++;
        //CurrentInstruction = memoryAccessor[EffectiveAddress];
    }
    
    private ushort CpuTick() 
        => CurrentInstruction switch 
        {
            // BRK.
            0x0 => CurrentInstructionStep switch
            {
                1 => BrkCycle1(),
                2 => BrkCycle2(),
                3 => BrkCycle3(),
                4 => BrkCycle4(),
                5 => BrkCycle5(),
                6 => BrkCycle6(),
                7 => BrkCycle7(),
                _ => throw new InvalidInstructionStepException(0x0)
            },
            // ORA indirect, X.
            0x1 => CurrentInstructionStep switch
            {
                1 => OraIndirectXCycle1(),
                2 => OraIndirectXCycle2(),
                3 => OraIndirectXCycle3(),
                4 => OraIndirectXCycle4(),
                5 => OraIndirectXCycle5(),
                6 => OraIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x1)
            },
            // Jam (illegal)
            0x2 => Jam(),
            // SLO indirect, X (illegal)
            0x3 => CurrentInstructionStep switch
            {
                1 => SloIndirectXCycle1(),
                2 => SloIndirectXCycle2(),
                3 => SloIndirectXCycle3(),
                4 => SloIndirectXCycle4(),
                5 => SloIndirectXCycle5(),
                6 => SloIndirectXCycle6(),
                7 => SloIndirectXCycle7(),
                8 => SloIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0x3)
            },
            // NOP zeropage (illegal)
            0x4 => CurrentInstructionStep switch
            {
                1 => NopZeropageCycle1(),
                2 => NopZeropageCycle2(),
                3 => NopZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x4)
            },
            // ORA zeropage
            0x5 => CurrentInstructionStep switch
            {
                1 => OraZeropageXCycle1(),
                2 => OraZeropageXCycle2(),
                3 => OraZeropageXCycle3(),
                4 => OraZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x5)
            },
            // ASL zeropage
            0x6 => CurrentInstructionStep switch
            {
                1 => AslZeropageCycle1(),
                2 => AslZeropageCycle2(),
                3 => AslZeropageCycle3(),
                4 => AslZeropageCycle4(),
                5 => AslZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0x6)
            },
            // SLO zeropage
            0x7 => CurrentInstructionStep switch
            {
                1 => SloZeropageCycle1(),
                2 => SloZeropageCycle2(),
                3 => SloZeropageCycle3(),
                4 => SloZeropageCycle4(),
                5 => SloZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0x7)
            },
            // PHP zeropage
            0x8 => CurrentInstructionStep switch
            {
                1 => PhpCycle1(),
                2 => PhpCycle2(),
                3 => PhpCycle3(),
                _ => throw new InvalidInstructionStepException(0x8)
            },
            // ORA imm
            0x9 => CurrentInstructionStep switch
            {
                1 => OraImmCycle1(),
                2 => OraImmCycle2(),
                _ => throw new InvalidInstructionStepException(0x9)
            },
            // ASL accum
            0xA => CurrentInstructionStep switch
            {
                1 => AslAccumCycle1(),
                2 => AslAccumCycle2(),
                _ => throw new InvalidInstructionStepException(0xA)
            },
            // ANC imm (illegal)
            0xB => CurrentInstructionStep switch
            {
                1 => AncCycle1(),
                2 => AncCycle2(),
                3 => AncCycle3(),
                _ => throw new InvalidInstructionStepException(0xB)
            },
            // Nop abs (illegal)
            0xC => CurrentInstructionStep switch
            {
                1 => NopAbsoluteCycle1(),
                2 => NopAbsoluteCycle2(),
                3 => NopAbsoluteCycle3(),
                4 => NopAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xC)
            },
            // Ora abs
            0xD => CurrentInstructionStep switch
            {
                1 => OraAbsoluteCycle1(),
                2 => OraAbsoluteCycle2(),
                3 => OraAbsoluteCycle3(),
                4 => OraAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xD)
            },
            // ASL abs
            0xE => CurrentInstructionStep switch
            {
                1 => AslAbsoluteCycle1(),
                2 => AslAbsoluteCycle2(),
                3 => AslAbsoluteCycle3(),
                4 => AslAbsoluteCycle4(),
                5 => AslAbsoluteCycle5(),
                6 => AslAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xE)
            },
            // SLO abs
            0xF => CurrentInstructionStep switch
            {
                1 => SloAbsoluteCycle1(),
                2 => SloAbsoluteCycle2(),
                3 => SloAbsoluteCycle3(),
                4 => SloAbsoluteCycle4(),
                5 => SloAbsoluteCycle5(),
                6 => SloAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xF)
            },
            // BPL
            0x10 => CurrentInstructionStep switch
            {
                1 => BplCycle1(),
                2 => BplCycle2(),
                3 => BplCycle3(),
                4 => BplCycle4(),
                _ => throw new InvalidInstructionStepException(0x11)
            },
            // ORA indirect, Y
            0x11 => CurrentInstructionStep switch
            {
                1 => OraIndirectYCycle1(),
                2 => OraIndirectYCycle2(),
                3 => OraIndirectYCycle3(),
                4 => OraIndirectYCycle4(),
                5 => OraIndirectYCycle5(),
                6 => OraIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0x12)
            },
            // JAM (illegal)
            0x12 => Jam(),
            // 0x13 SLO indirect, Y
            0x13 => CurrentInstructionStep switch
            {
                1 => SloIndirectYCycle1(),
                2 => SloIndirectYCycle2(),
                3 => SloIndirectYCycle3(),
                4 => SloIndirectYCycle4(),
                5 => SloIndirectYCycle5(),
                6 => SloIndirectYCycle6(),
                7 => SloIndirectYCycle7(),
                8 => SloIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0x13)
            },       
            // 0x14 NOP zeropage, X (illegal)
            0x14 => CurrentInstructionStep switch
            {
                1 => NopZeropageCycle1(),
                2 => NopZeropageCycle2(),
                3 => NopZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x14)
            },       
            // 0x15 ORA zeropage, X
            0x15 => CurrentInstructionStep switch
            {
                1 => OraZeropageXCycle1(),
                2 => OraZeropageXCycle2(),
                3 => OraZeropageXCycle3(),
                4 => OraZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x15)
            },       
            // 0x16 ASL zeropage, X
            0x16 => CurrentInstructionStep switch
            {
                1 => AslZeropageXCycle1(),
                2 => AslZeropageXCycle2(),
                3 => AslZeropageXCycle3(),
                4 => AslZeropageXCycle4(),
                5 => AslZeropageXCycle5(),
                6 => AslZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x16)
            },       
            // 0x17 SLO zeropage
            0x17 => CurrentInstructionStep switch
            {
                1 => SloZeropageCycle1(),
                2 => SloZeropageCycle2(),
                3 => SloZeropageCycle3(),
                4 => SloZeropageCycle4(),
                5 => SloZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0x17)
            },       
            // 0x18 Clc
            0x18 => CurrentInstructionStep switch
            {
                1 => ClcCycle1(),
                2 => ClcCycle2(),
                _ => throw new InvalidInstructionStepException(0x18)
            },       
            // 0x19 ORA absolute, Y
            0x19 => CurrentInstructionStep switch
            {
                1 => OraAbsoluteCycle1(),
                2 => OraAbsoluteCycle2(),
                3 => OraAbsoluteCycle3(),
                4 => OraAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x19)
            },       
            // 0x1A NOP implied (illegal)
            0x1A => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(),
                2 => NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0x1A)
            },       
            // 0x1B NOP implied (illegal)
            0x1B => CurrentInstructionStep switch
            {
                1 => SloAbsoluteYCycle1(),
                2 => SloAbsoluteYCycle2(),
                3 => SloAbsoluteYCycle3(),
                4 => SloAbsoluteYCycle4(),
                5 => SloAbsoluteYCycle5(),
                6 => SloAbsoluteYCycle6(),
                7 => SloAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x1B)
            },       
            // 0x1C NOP abs, X (illegal)
            0x1C => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(),
                2 => NopAbsoluteXCycle2(),
                3 => NopAbsoluteXCycle3(),
                4 => NopAbsoluteXCycle4(),
                5 => NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x1C)
            },       
            // 0x1D ORA abs, X
            0x1D => CurrentInstructionStep switch
            {
                1 => OraAbsoluteXCycle1(),
                2 => OraAbsoluteXCycle2(),
                3 => OraAbsoluteXCycle3(),
                4 => OraAbsoluteXCycle4(),
                5 => OraAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x1D)
            },       
            // 0x1E ASL abs, X
            0x1E => CurrentInstructionStep switch
            {
                1 => AslAbsoluteXCycle1(),
                2 => AslAbsoluteXCycle2(),
                3 => AslAbsoluteXCycle3(),
                4 => AslAbsoluteXCycle4(),
                5 => AslAbsoluteXCycle5(),
                6 => AslAbsoluteXCycle6(),
                7 => AslAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x1E)
            },       
            // 0x1F SLO abs, X (illegal)
            0x1F => CurrentInstructionStep switch
            {
                1 => SloAbsoluteXCycle1(),
                2 => SloAbsoluteXCycle2(),
                3 => SloAbsoluteXCycle3(),
                4 => SloAbsoluteXCycle4(),
                5 => SloAbsoluteXCycle5(),
                6 => SloAbsoluteXCycle6(),
                7 => SloAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x1F)
            },       
            // 0x20 JSR
            0x20 => CurrentInstructionStep switch
            {
                1 => JsrCycle1(),
                2 => JsrCycle2(),
                3 => JsrCycle3(),
                4 => JsrCycle4(),
                5 => JsrCycle5(),
                6 => JsrCycle6(),
                _ => throw new InvalidInstructionStepException(0x20)
            },       
            // 0x21 AND X, indirect
            0x21 => CurrentInstructionStep switch
            {
                1 => AndIndirectYCycle1(),
                2 => AndIndirectYCycle2(),
                3 => AndIndirectYCycle3(),
                4 => AndIndirectYCycle4(),
                5 => AndIndirectYCycle5(),
                6 => AndIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0x21)
            },       
            // 0x22 JAM (illegal)
            0x22 => Jam(),
            // 0x23 RLA X, indirect
            0x23 => CurrentInstructionStep switch
            {
                1 => RlaIndirectYCycle1(),
                2 => RlaIndirectYCycle2(),
                3 => RlaIndirectYCycle3(),
                4 => RlaIndirectYCycle4(),
                5 => RlaIndirectYCycle5(),
                6 => RlaIndirectYCycle6(),
                7 => RlaIndirectYCycle7(),
                8 => RlaIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0x23)
            },       
            // 0x24 BIT zeropage
            0x24 => CurrentInstructionStep switch
            {
                1 => BitZeropageCycle1(),
                2 => BitZeropageCycle2(),
                3 => BitZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x24)
            },       
            // 0x25 AND zeropage
            0x25 => CurrentInstructionStep switch
            {
                1 => AndZeropageCycle1(), 
                2 => AndZeropageCycle2(),
                3 => AndZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x25)
            },       
            // 0x26 ROL zeropage
            0x26 => CurrentInstructionStep switch
            {
                1 => RolZeropageCycle1(), 
                2 => RolZeropageCycle2(),
                3 => RolZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x26)
            },       
            // 0x27 RLA zeropage (illegal)
            0x27 => CurrentInstructionStep switch
            {
                1 => RlaZeropageCycle1(), 
                2 => RlaZeropageCycle2(), 
                3 => RlaZeropageCycle3(), 
                4 => RlaZeropageCycle4(), 
                5 => RlaZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x27)
            },
            // 0x28 PHA
            0x28 => CurrentInstructionStep switch
            {
                1 => PhaCycle1(), 
                2 => PhaCycle2(), 
                3 => PhaCycle3(), 
                _ => throw new InvalidInstructionStepException(0x28)
            },
            // 0x29 AND imd
            0x29 => CurrentInstructionStep switch
            {
                1 => AndImmCycle1(), 
                2 => AndImmCycle2(), 
                _ => throw new InvalidInstructionStepException(0x29)
            },
            // 0x2A ROL accum
            0x2A => CurrentInstructionStep switch
            {
                1 => RolAccumCycle1(), 
                2 => RolAccumCycle2(), 
                _ => throw new InvalidInstructionStepException(0x2A)
            },
            // 0x2B ANC (illegal)
            0x2B => CurrentInstructionStep switch
            {
                1 => AncCycle1(), 
                2 => AncCycle2(), 
                3 => AncCycle3(), 
                _ => throw new InvalidInstructionStepException(0x2B)
            },
            // 0x2C BIT abs
            0x2C => CurrentInstructionStep switch
            {
                1 => BitAbsoluteCycle1(), 
                2 => BitAbsoluteCycle2(), 
                3 => BitAbsoluteCycle3(), 
                4 => BitAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x2C)
            },
            // 0x2D AND abs
            0x2D => CurrentInstructionStep switch
            {
                1 => AndAbsoluteCycle1(), 
                2 => AndAbsoluteCycle2(), 
                3 => AndAbsoluteCycle3(), 
                4 => AndAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x2D)
            },
            // 0x2E ROL abs
            0x2E => CurrentInstructionStep switch
            {
                1 => RolAbsoluteCycle1(), 
                2 => RolAbsoluteCycle2(), 
                3 => RolAbsoluteCycle3(), 
                4 => RolAbsoluteCycle4(), 
                5 => RolAbsoluteCycle5(), 
                6 => RolAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x2E)
            },
            // 0x2F RLA abs (illegal)
            0x2F => CurrentInstructionStep switch
            {
                1 => RlaAbsoluteCycle1(), 
                2 => RlaAbsoluteCycle2(), 
                3 => RlaAbsoluteCycle3(), 
                4 => RlaAbsoluteCycle4(), 
                5 => RlaAbsoluteCycle5(), 
                6 => RlaAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x2F)
            },
            // 0x30 BMI
            0x30 => CurrentInstructionStep switch
            {
                1 => BmiCycle1(), 
                2 => BmiCycle2(), 
                3 => BmiCycle3(), 
                4 => BmiCycle4(), 
                _ => throw new InvalidInstructionStepException(0x30)
            },
            // 0x31 AND ind, Y
            0x31 => CurrentInstructionStep switch
            {
                1 => AndIndirectYCycle1(), 
                2 => AndIndirectYCycle2(), 
                3 => AndIndirectYCycle3(), 
                4 => AndIndirectYCycle4(),  
                5 => AndIndirectYCycle5(), 
                6 => AndIndirectYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x31)
            },
            // 0x32 JAM (illegal)
            0x32 => Jam(),
            // 0x33 RLA ind, Y (illegal)
            0x33 => CurrentInstructionStep switch
            {
                1 => RlaIndirectYCycle1(), 
                2 => RlaIndirectYCycle2(), 
                3 => RlaIndirectYCycle3(), 
                4 => RlaIndirectYCycle4(),  
                5 => RlaIndirectYCycle5(), 
                6 => RlaIndirectYCycle6(), 
                7 => RlaIndirectYCycle7(), 
                8 => RlaIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x33)
            },
            // 0x34 NOP zeropage, X
            0x34 => CurrentInstructionStep switch
            {
                1 => NopZeropageXCycle1(), 
                2 => NopZeropageXCycle2(), 
                3 => NopZeropageXCycle3(), 
                4 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x34)
            },
            // 0x35 AND zeropage, X
            0x35 => CurrentInstructionStep switch
            {
                1 => AndZeropageCycle1(), 
                2 => AndZeropageCycle2(), 
                3 => AndZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x35)
            },
            // 0x36 ROL zeropage, X
            0x36 => CurrentInstructionStep switch
            {
                1 => RolZeropageCycle1(), 
                2 => RolZeropageCycle2(), 
                3 => RolZeropageCycle3(), 
                4 => RolZeropageCycle4(), 
                5 => RolZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x36)
            },
            // 0x37 RLA zeropage, X (illegal)
            0x37 => CurrentInstructionStep switch
            {
                1 => RlaZeropageCycle1(), 
                2 => RlaZeropageCycle2(), 
                3 => RlaZeropageCycle3(), 
                4 => RlaZeropageCycle4(), 
                5 => RlaZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x37)
            },
            // 0x38 SEC implied
            0x38 => CurrentInstructionStep switch
            {
                1 => SecCycle1(), 
                2 => SecCycle2(), 
                _ => throw new InvalidInstructionStepException(0x38)
            },
            // 0x39 And abs, Y
            0x39 => CurrentInstructionStep switch
            {
                1 => AndAbsoluteYCycle1(), 
                2 => AndAbsoluteYCycle2(), 
                3 => AndAbsoluteYCycle3(), 
                4 => AndAbsoluteYCycle4(), 
                5 => AndAbsoluteYCycle5(), 
                _ => throw new InvalidInstructionStepException(0x39)
            },
            // 0x3A NOP implied (illegal)
            0x3A => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(), 
                2 => NopImpliedCycle2(), 
                _ => throw new InvalidInstructionStepException(0x3A)
            },
            // 0x3B RLA abs, Y (illegal)
            0x3B => CurrentInstructionStep switch
            {
                1 => RlaAbsoluteYCycle1(), 
                2 => RlaAbsoluteYCycle2(), 
                3 => RlaAbsoluteYCycle3(), 
                4 => RlaAbsoluteYCycle4(), 
                5 => RlaAbsoluteYCycle5(),
                6 => RlaAbsoluteYCycle6(),
                7 => RlaAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x3B)
            },
            // 0x3C NOP abs, X (illegal)
            0x3C => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(), 
                2 => NopAbsoluteXCycle2(), 
                3 => NopAbsoluteXCycle3(), 
                4 => NopAbsoluteXCycle4(), 
                5 => NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x3C)
            },
            // 0x3D AND abs, X
            0x3D => CurrentInstructionStep switch
            {
                1 => AndAbsoluteXCycle1(), 
                2 => AndAbsoluteXCycle2(), 
                3 => AndAbsoluteXCycle3(), 
                4 => AndAbsoluteXCycle4(), 
                5 => AndAbsoluteXCycle5(), 
                _ => throw new InvalidInstructionStepException(0x3D)
            },
            // 0x3E ROL abs, X
            0x3E => CurrentInstructionStep switch
            {
                1 => RolAbsoluteXCycle1(), 
                2 => RolAbsoluteXCycle2(), 
                3 => RolAbsoluteXCycle3(), 
                4 => RolAbsoluteXCycle4(), 
                5 => RolAbsoluteXCycle5(), 
                6 => RolAbsoluteXCycle6(), 
                7 => RolAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x3E)
            },
            // 0x3F RLA abs, X (illegal)
            0x3F => CurrentInstructionStep switch
            {
                1 => RlaAbsoluteXCycle1(), 
                2 => RlaAbsoluteXCycle2(), 
                3 => RlaAbsoluteXCycle3(), 
                4 => RlaAbsoluteXCycle4(), 
                5 => RlaAbsoluteXCycle5(), 
                6 => RlaAbsoluteXCycle6(), 
                7 => RlaAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x3F)
            },
            // 0x40 RTI implied
            0x40 => CurrentInstructionStep switch
            {
                1 => RtiCycle1(), 
                2 => RtiCycle2(), 
                3 => RtiCycle3(), 
                4 => RtiCycle4(), 
                5 => RtiCycle5(), 
                6 => RtiCycle6(), 
                _ => throw new InvalidInstructionStepException(0x40)
            },
            // 0x41 EOR indirect, X
            0x41 => CurrentInstructionStep switch
            {
                1 => EorIndirectXCycle1(), 
                2 => EorIndirectXCycle2(), 
                3 => EorIndirectXCycle3(), 
                4 => EorIndirectXCycle4(), 
                5 => EorIndirectXCycle5(), 
                6 => EorIndirectXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x41)
            },
            // 0x42 JAM (illegal)
            0x42 => Jam(),
            // 0x43 Sre indirect, X
            0x43 => CurrentInstructionStep switch
            {
                1 => SreIndirectXCycle1(), 
                2 => SreIndirectXCycle2(), 
                3 => SreIndirectXCycle3(), 
                4 => SreIndirectXCycle4(), 
                5 => SreIndirectXCycle5(), 
                6 => SreIndirectXCycle6(), 
                7 => SreIndirectXCycle7(), 
                8 => SreIndirectXCycle8(), 
                _ => throw new InvalidInstructionStepException(0x43)
            },
            // 0x44 NOP zeropage (illegal)
            0x44 => CurrentInstructionStep switch
            {
                1 => NopZeropageCycle1(), 
                2 => NopZeropageCycle2(), 
                3 => NopZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x44)
            },
            // 0x45 EOR zeropage
            0x45 => CurrentInstructionStep switch
            {
                1 => EorZeropageCycle1(), 
                2 => EorZeropageCycle2(), 
                3 => EorZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x45)
            },
            // 0x46 LSR zeropage
            0x46 => CurrentInstructionStep switch
            {
                1 => LsrZeropageCycle1(), 
                2 => LsrZeropageCycle2(), 
                3 => LsrZeropageCycle3(), 
                4 => LsrZeropageCycle4(), 
                5 => LsrZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x46)
            },
            // 0x47 SRE zeropage (illegal)
            0x47 => CurrentInstructionStep switch
            {
                1 => SreZeropageCycle1(), 
                2 => SreZeropageCycle2(), 
                3 => SreZeropageCycle3(), 
                4 => SreZeropageCycle4(), 
                5 => SreZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x47)
            },
            // 0x48 PHA imp
            0x48 => CurrentInstructionStep switch
            {
                1 => PhaCycle1(), 
                2 => PhaCycle2(), 
                3 => PhaCycle3(), 
                _ => throw new InvalidInstructionStepException(0x48)
            },
            // 0x49 EOR imm
            0x49 => CurrentInstructionStep switch
            {
                1 => EorImmCycle1(), 
                2 => EorImmCycle2(), 
                _ => throw new InvalidInstructionStepException(0x49)
            },
            // 0x4A LSR accum
            0x4A => CurrentInstructionStep switch
            {
                1 => LsrAccumCycle1(), 
                2 => LsrAccumCycle2(), 
                _ => throw new InvalidInstructionStepException(0x4A)
            },
            // 0x4B ALR (illegal)
            0x4B => CurrentInstructionStep switch
            {
                1 => AlrCycle1(), 
                2 => AlrCycle2(), 
                _ => throw new InvalidInstructionStepException(0x4B)
            },
            // 0x4C JMP abs
            0x4C => CurrentInstructionStep switch
            {
                1 => JmpAbsoluteCycle1(), 
                2 => JmpAbsoluteCycle2(), 
                3 => JmpAbsoluteCycle3(), 
                4 => JmpAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x4C)
            },
            // 0x4D EOR abs, X
            0x4D => CurrentInstructionStep switch
            {
                1 => EorAbsoluteXCycle1(), 
                2 => EorAbsoluteXCycle2(), 
                3 => EorAbsoluteXCycle3(), 
                4 => EorAbsoluteXCycle4(), 
                5 => EorAbsoluteXCycle5(), 
                _ => throw new InvalidInstructionStepException(0x4D)
            },
            // 0x4E LSR abs
            0x4E => CurrentInstructionStep switch
            {
                1 => LsrAbsoluteCycle1(), 
                2 => LsrAbsoluteCycle2(), 
                3 => LsrAbsoluteCycle3(), 
                4 => LsrAbsoluteCycle4(), 
                5 => LsrAbsoluteCycle5(), 
                6 => LsrAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x4E)
            },
            // 0x4F SRE abs (illegal)
            0x4F => CurrentInstructionStep switch
            {
                1 => SreAbsoluteCycle1(), 
                2 => SreAbsoluteCycle2(), 
                3 => SreAbsoluteCycle3(), 
                4 => SreAbsoluteCycle4(), 
                5 => SreAbsoluteCycle5(), 
                6 => SreAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x4F)
            },
            // 0x50 BVC rel
            0x50 => CurrentInstructionStep switch
            {
                1 => BccCycle1(), 
                2 => BccCycle1(), 
                3 => BccCycle1(), 
                4 => BccCycle1(), 
                _ => throw new InvalidInstructionStepException(0x50)
            },
            // 0x51 BVC rel
            0x51 => CurrentInstructionStep switch
            {
                1 => EorIndirectYCycle1(), 
                2 => EorIndirectYCycle2(), 
                3 => EorIndirectYCycle3(), 
                4 => EorIndirectYCycle4(), 
                5 => EorIndirectYCycle5(), 
                6 => EorIndirectYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x51)
            },
            // 0x52 JAM
            0x52 => Jam(),
            // 0x53 SRE indirect, Y
            0x53 => CurrentInstructionStep switch
            {
                1 => SreIndirectYCycle1(), 
                2 => SreIndirectYCycle2(), 
                3 => SreIndirectYCycle3(), 
                4 => SreIndirectYCycle4(), 
                5 => SreIndirectYCycle5(), 
                6 => SreIndirectYCycle6(), 
                7 => SreIndirectYCycle7(), 
                8 => SreIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x53)
            },
            // 0x54 NOP zeropage, X
            0x54 => CurrentInstructionStep switch
            {
                1 => NopZeropageXCycle1(), 
                2 => NopZeropageXCycle2(), 
                3 => NopZeropageXCycle3(), 
                4 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x54)
            },
            // 0x55 EOR zeropage, X
            0x55 => CurrentInstructionStep switch
            {
                1 => EorZeropageXCycle1(), 
                2 => EorZeropageXCycle2(), 
                3 => EorZeropageXCycle3(), 
                4 => EorZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x55)
            },
            // 0x56 LSR zeropage, X
            0x56 => CurrentInstructionStep switch
            {
                1 => LsrZeropageXCycle1(), 
                2 => LsrZeropageXCycle2(), 
                3 => LsrZeropageXCycle3(), 
                4 => LsrZeropageXCycle4(), 
                5 => LsrZeropageXCycle5(), 
                6 => LsrZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x56)
            },
            // 0x57 SRE zeropage, X
            0x57 => CurrentInstructionStep switch
            {
                1 => SreAbsoluteXCycle1(), 
                2 => SreAbsoluteXCycle2(), 
                3 => SreAbsoluteXCycle3(), 
                4 => SreAbsoluteXCycle4(), 
                5 => SreAbsoluteXCycle5(), 
                6 => SreAbsoluteXCycle6(), 
                7 => SreAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x57)
            },
            // 0x58 CLI impl
            0x58 => CurrentInstructionStep switch
            {
                1 => CliCycle1(), 
                2 => CliCycle2(), 
                _ => throw new InvalidInstructionStepException(0x58)
            },
            // 0x59 EOR abs, Y
            0x59 => CurrentInstructionStep switch
            {
                1 => EorAbsoluteYCycle1(), 
                2 => EorAbsoluteYCycle2(), 
                3 => EorAbsoluteYCycle3(), 
                4 => EorAbsoluteYCycle4(), 
                5 => EorAbsoluteYCycle5(), 
                _ => throw new InvalidInstructionStepException(0x59)
            },
            // 0x5A NOP impl
            0x5A => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(), 
                2 => NopImpliedCycle2(), 
                _ => throw new InvalidInstructionStepException(0x5A)
            },
            // 0x5B SRE abs, Y
            0x5B => CurrentInstructionStep switch
            {
                1 => SreAbsoluteYCycle1(), 
                2 => SreAbsoluteYCycle2(), 
                3 => SreAbsoluteYCycle3(), 
                4 => SreAbsoluteYCycle4(), 
                5 => SreAbsoluteYCycle5(), 
                6 => SreAbsoluteYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x5B)
            },
            // 0x5C NOP abs, X
            0x5C => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(), 
                2 => NopAbsoluteXCycle2(), 
                3 => NopAbsoluteXCycle3(), 
                4 => NopAbsoluteXCycle4(), 
                5 => NopAbsoluteXCycle5(), 
                _ => throw new InvalidInstructionStepException(0x5C)
            },
            // 0x5D EOR abs, X
            0x5D => CurrentInstructionStep switch
            {
                1 => EorAbsoluteXCycle1(), 
                2 => EorAbsoluteXCycle2(), 
                3 => EorAbsoluteXCycle3(), 
                4 => EorAbsoluteXCycle4(), 
                5 => EorAbsoluteXCycle5(), 
                _ => throw new InvalidInstructionStepException(0x5D)
            },
            // 0x5E LSR abs, X
            0x5E => CurrentInstructionStep switch
            {
                1 => LsrAbsoluteXCycle1(), 
                2 => LsrAbsoluteXCycle2(), 
                3 => LsrAbsoluteXCycle3(), 
                4 => LsrAbsoluteXCycle4(), 
                5 => LsrAbsoluteXCycle5(), 
                6 => LsrAbsoluteXCycle6(), 
                7 => LsrAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x5E)
            },
            // 0x5F SRE abs, X
            0x5F => CurrentInstructionStep switch
            {
                1 => SreAbsoluteXCycle1(), 
                2 => SreAbsoluteXCycle2(), 
                3 => SreAbsoluteXCycle3(), 
                4 => SreAbsoluteXCycle4(), 
                5 => SreAbsoluteXCycle5(), 
                6 => SreAbsoluteXCycle6(), 
                7 => SreAbsoluteXCycle7(), 
                _ => throw new InvalidInstructionStepException(0x5F)
            },
            // 0x60 RTS impl
            0x60 => CurrentInstructionStep switch
            {
                1 => RtsCycle1(), 
                2 => RtsCycle2(), 
                3 => RtsCycle3(), 
                4 => RtsCycle4(), 
                5 => RtsCycle5(), 
                6 => RtsCycle6(), 
                _ => throw new InvalidInstructionStepException(0x60)
            },
            // 0x61 ADC indirect, X
            0x61 => CurrentInstructionStep switch
            {
                1 => AdcIndirectXCycle1(), 
                2 => AdcIndirectXCycle2(), 
                3 => AdcIndirectXCycle3(), 
                4 => AdcIndirectXCycle4(), 
                5 => AdcIndirectXCycle5(), 
                6 => AdcIndirectXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x61)
            },
            // 0x62 JAM
            0x62 => Jam(),
            // 0x63 RRA X, indirect
            0x63 => CurrentInstructionStep switch
            {
                1 => RraIndirectXCycle1(), 
                2 => RraIndirectXCycle2(), 
                3 => RraIndirectXCycle3(), 
                4 => RraIndirectXCycle4(), 
                5 => RraIndirectXCycle5(), 
                6 => RraIndirectXCycle6(), 
                7 => RraIndirectXCycle7(), 
                8 => RraIndirectXCycle8(), 
                _ => throw new InvalidInstructionStepException(0x63)
            },
            // 0x64 NOP zeropage
            0x64 => CurrentInstructionStep switch
            {
                1 => NopZeropageXCycle1(), 
                2 => NopZeropageXCycle2(), 
                3 => NopZeropageXCycle3(), 
                4 => NopZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x64)
            },
            // 0x65 ADC zeropage
            0x65 => CurrentInstructionStep switch
            {
                1 => AdcZeropageCycle1(), 
                2 => AdcZeropageCycle2(), 
                3 => AdcZeropageCycle3(), 
                _ => throw new InvalidInstructionStepException(0x65)
            },
            // 0x66 ROR zeropage
            0x66 => CurrentInstructionStep switch
            {
                1 => RorZeropageCycle1(), 
                2 => RorZeropageCycle2(), 
                3 => RorZeropageCycle3(), 
                4 => RorZeropageCycle4(), 
                5 => RorZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x66)
            },
            // 0x67 RRA zeropage
            0x67 => CurrentInstructionStep switch
            {
                1 => RraZeropageCycle1(), 
                2 => RraZeropageCycle2(), 
                3 => RraZeropageCycle3(), 
                4 => RraZeropageCycle4(), 
                5 => RraZeropageCycle5(), 
                _ => throw new InvalidInstructionStepException(0x67)
            },
            // 0x68 PLA impl
            0x68 => CurrentInstructionStep switch
            {
                1 => PlaCycle1(), 
                2 => PlaCycle2(), 
                _ => throw new InvalidInstructionStepException(0x68)
            },
            // 0x69 ADC imm
            0x69 => CurrentInstructionStep switch
            {
                1 => AdcImmCycle1(), 
                2 => AdcImmCycle2(), 
                _ => throw new InvalidInstructionStepException(0x69)
            },
            // 0x6A ROR abs
            0x6A => CurrentInstructionStep switch
            {
                1 => AdcAbsoluteCycle1(), 
                2 => AdcAbsoluteCycle2(), 
                3 => AdcAbsoluteCycle3(), 
                4 => AdcAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x6A)
            },
            // 0x6B Arr imm
            0x6B => CurrentInstructionStep switch
            {
                1 => ArrCycle1(), 
                2 => ArrCycle2(), 
                _ => throw new InvalidInstructionStepException(0x6B)
            },
            // 0x6C JMP indirect
            0x6C => CurrentInstructionStep switch
            {
                1 => JmpIndirectCycle1(), 
                2 => JmpIndirectCycle2(), 
                3 => JmpIndirectCycle3(), 
                4 => JmpIndirectCycle4(), 
                5 => JmpIndirectCycle5(), 
                _ => throw new InvalidInstructionStepException(0x6C)
            },
            // 0x6D ADC abs
            0x6D => CurrentInstructionStep switch
            {
                1 => AdcAbsoluteCycle1(), 
                2 => AdcAbsoluteCycle2(), 
                3 => AdcAbsoluteCycle3(), 
                4 => AdcAbsoluteCycle4(), 
                _ => throw new InvalidInstructionStepException(0x6D)
            },
            // 0x6E ROR abs
            0x6E => CurrentInstructionStep switch
            {
                1 => RorAbsoluteCycle1(), 
                2 => RorAbsoluteCycle2(), 
                3 => RorAbsoluteCycle3(), 
                4 => RorAbsoluteCycle4(), 
                5 => RorAbsoluteCycle5(), 
                6 => RorAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x6E)
            },
            // 0x6F RRA abs
            0x6F => CurrentInstructionStep switch
            {
                1 => RraAbsoluteCycle1(), 
                2 => RraAbsoluteCycle2(), 
                3 => RraAbsoluteCycle3(), 
                4 => RraAbsoluteCycle4(), 
                5 => RraAbsoluteCycle5(), 
                6 => RraAbsoluteCycle6(), 
                _ => throw new InvalidInstructionStepException(0x6F)
            },
            // 0x70 BVS rel
            0x70 => CurrentInstructionStep switch
            {
                1 => BvsCycle1(), 
                2 => BvsCycle2(), 
                3 => BvsCycle3(), 
                4 => BvsCycle4(), 
                _ => throw new InvalidInstructionStepException(0x70)
            },
            // 0x71 ADC ind, Y
            0x71 => CurrentInstructionStep switch
            {
                1 => AdcIndirectYCycle1(), 
                2 => AdcIndirectYCycle2(), 
                3 => AdcIndirectYCycle3(), 
                4 => AdcIndirectYCycle4(), 
                5 => AdcIndirectYCycle5(), 
                6 => AdcIndirectYCycle6(), 
                _ => throw new InvalidInstructionStepException(0x71)
            },
            // 0x72 JAM
            0x72 => Jam(),
            // 0x73 RRA ind, Y
            0x73 => CurrentInstructionStep switch
            {
                1 => RraIndirectYCycle1(), 
                2 => RraIndirectYCycle2(), 
                3 => RraIndirectYCycle3(), 
                4 => RraIndirectYCycle4(), 
                5 => RraIndirectYCycle5(), 
                6 => RraIndirectYCycle6(), 
                7 => RraIndirectYCycle7(), 
                8 => RraIndirectYCycle8(), 
                _ => throw new InvalidInstructionStepException(0x73)
            },
            // 0x74 NOP zeropage, X
            0x74 => CurrentInstructionStep switch
            {
                1 => NopZeropageCycle1(),
                2 => NopZeropageCycle2(),
                3 => NopZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x74)
            },       
            // 0x75 ADC zeropage, X
            0x75 => CurrentInstructionStep switch
            {
                1 => AdcZeropageXCycle1(),
                2 => AdcZeropageXCycle2(),
                3 => AdcZeropageXCycle3(),
                4 => AdcZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x75)
            },       
            // 0x76 ROR zeropage, X
            0x76 => CurrentInstructionStep switch
            {
                1 => RorZeropageXCycle1(),
                2 => RorZeropageXCycle2(),
                3 => RorZeropageXCycle3(),
                4 => RorZeropageXCycle4(),
                5 => RorZeropageXCycle5(),
                6 => RorZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x76)
            },       
            // 0x76 RRA zeropage, X
            0x77 => CurrentInstructionStep switch
            {
                1 => RraZeropageXCycle1(),
                2 => RraZeropageXCycle2(),
                3 => RraZeropageXCycle3(),
                4 => RraZeropageXCycle4(),
                5 => RraZeropageXCycle5(),
                6 => RraZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x76)
            },       
        };

    protected ushort AlrCycle2()
    {
        return 0;
    }

    protected ushort AlrCycle1()
    {
        return 0;
    }

    // usbc
    protected ushort UsbcCycle2()
    {
        return 0;
    }

    protected ushort UsbcCycle1()
    {
        return 0;
    }

    // sbx
    protected ushort SbxCycle2()
    {
        return 0;
    }

    protected ushort SbxCycle1()
    {
        return 0;
    }

    // isc
    protected ushort IscZeropageCycle5()
    {
        return 0;
    }

    protected ushort IscZeropageCycle4()
    {
        return 0;
    }

    protected ushort IscZeropageCycle3()
    {
        return 0;
    }

    protected ushort IscZeropageCycle2()
    {
        return 0;
    }
    protected ushort IscZeropageCycle1()
    {
        return 0;
    }


    protected ushort IscZeropageXCycle6()
    {
        return 0;
    }
    protected ushort IscZeropageXCycle5()
    {
        return 0;
    }
    protected ushort IscZeropageXCycle4()
    {
        return 0;
    }
    protected ushort IscZeropageXCycle3()
    {
        return 0;
    }
    protected ushort IscZeropageXCycle2()
    {
        return 0;
    }
    protected ushort IscZeropageXCycle1()
    {
        return 0;
    }

    protected ushort IscAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort IscAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort IscAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort IscAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort IscAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort IscAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort IscAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort IscAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort IscAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort IscAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort IscIndirectXCycle8()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle7()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle6()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle5()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle4()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle3()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle2()
    {
        return 0;
    }
    protected ushort IscIndirectXCycle1()
    {
        return 0;
    }

    protected ushort IscIndirectYCycle8()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle7()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle6()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle5()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle4()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle3()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle2()
    {
        return 0;
    }
    protected ushort IscIndirectYCycle1()
    {
        return 0;
    }

    // dcp
    protected ushort DcpZeropageCycle5()
    {
        return 0;
    }
    protected ushort DcpZeropageCycle4()
    {
        return 0;
    }
    protected ushort DcpZeropageCycle3()
    {
        return 0;
    }
    protected ushort DcpZeropageCycle2()
    {
        return 0;
    }
    protected ushort DcpZeropageCycle1()
    {
        return 0;
    }


    protected ushort DcpZeropageXCycle6()
    {
        return 0;
    }
    protected ushort DcpZeropageXCycle5()
    {
        return 0;
    }
    protected ushort DcpZeropageXCycle4()
    {
        return 0;
    }
    protected ushort DcpZeropageXCycle3()
    {
        return 0;
    }
    protected ushort DcpZeropageXCycle2()
    {
        return 0;
    }
    protected ushort DcpZeropageXCycle1()
    {
        return 0;
    }

    protected ushort DcpAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort DcpAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort DcpAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort DcpAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort DcpAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort DcpAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort DcpAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort DcpAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort DcpAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort DcpAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort DcpIndirectXCycle8()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle7()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle6()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle5()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle4()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle3()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle2()
    {
        return 0;
    }
    protected ushort DcpIndirectXCycle1()
    {
        return 0;
    }

    protected ushort DcpIndirectYCycle8()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle7()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle6()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle5()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle4()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle3()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle2()
    {
        return 0;
    }
    protected ushort DcpIndirectYCycle1()
    {
        return 0;
    }

    // las

    protected ushort LasCycle4()
    {
        return 0;
    }
    protected ushort LasCycle3()
    {
        return 0;
    }
    protected ushort LasCycle2()
    {
        return 0;
    }
    protected ushort LasCycle1()
    {
        return 0;
    }

    // lxa
    protected ushort LxaCycle2()
    {
        return 0;
    }
    protected ushort LxaCycle1()
    {
        return 0;
    }

    // lax

    protected ushort LaxZeropageCycle3()
    {
        return 0;
    }
    protected ushort LaxZeropageCycle2()
    {
        return 0;
    }
    protected ushort LaxZeropageCycle1()
    {
        return 0;
    }


    protected ushort LaxZeropageYCycle4()
    {
        return 0;
    }
    protected ushort LaxZeropageYCycle3()
    {
        return 0;
    }
    protected ushort LaxZeropageYCycle2()
    {
        return 0;
    }
    protected ushort LaxZeropageYCycle1()
    {
        return 0;
    }

    protected ushort LaxAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort LaxAbsoluteCycle3()
    {
        return 0;
    }

    protected ushort LaxAbsoluteCycle2()
    {
        return 0;
    }

    protected ushort LaxAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort LaxAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort LaxAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort LaxAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort LaxAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort LaxAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort LaxIndirectXCycle6()
    {
        return 0;
    }
    protected ushort LaxIndirectXCycle5()
    {
        return 0;
    }
    protected ushort LaxIndirectXCycle4()
    {
        return 0;
    }
    protected ushort LaxIndirectXCycle3()
    {
        return 0;
    }
    protected ushort LaxIndirectXCycle2()
    {
        return 0;
    }
    protected ushort LaxIndirectXCycle1()
    {
        return 0;
    }

    protected ushort LaxIndirectYCycle6()
    {
        return 0;
    }
    protected ushort LaxIndirectYCycle5()
    {
        return 0;
    }
    protected ushort LaxIndirectYCycle4()
    {
        return 0;
    }
    protected ushort LaxIndirectYCycle3()
    {
        return 0;
    }
    protected ushort LaxIndirectYCycle2()
    {
        return 0;
    }
    protected ushort LaxIndirectYCycle1()
    {
        return 0;
    }

    // shx
    protected ushort ShxCycle5()
    {
        return 0;
    }
    protected ushort ShxCycle4()
    {
        return 0;
    }
    protected ushort ShxCycle3()
    {
        return 0;
    }
    protected ushort ShxCycle2()
    {
        return 0;
    }
    protected ushort ShxCycle1()
    {
        return 0;
    }

    // shy
    protected ushort ShyCycle5()
    {
        return 0;
    }
    protected ushort ShyCycle4()
    {
        return 0;
    }
    protected ushort ShyCycle3()
    {
        return 0;
    }
    protected ushort ShyCycle2()
    {
        return 0;
    }
    protected ushort ShyCycle1()
    {
        return 0;
    }

    // tas
    protected ushort TasCycle5()
    {
        return 0;
    }
    protected ushort TasCycle4()
    {
        return 0;
    }
    protected ushort TasCycle3()
    {
        return 0;
    }
    protected ushort TasCycle2()
    {
        return 0;
    }
    protected ushort TasCycle1()
    {
        return 0;
    }

    // SHA
    protected ushort ShaIndirectYCycle5()
    {
        return 0;
    }
    protected ushort ShaIndirectYCycle4()
    {
        return 0;
    }
    protected ushort ShaIndirectYCycle3()
    {
        return 0;
    }
    protected ushort ShaIndirectYCycle2()
    {
        return 0;
    }
    protected ushort ShaIndirectYCycle1()
    {
        return 0;
    }

    protected ushort ShaAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort ShaAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort ShaAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort ShaAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort ShaAbsoluteYCycle1()
    {
        return 0;
    }

    // JAM (illegal opcode).
    protected ushort Jam()
    {
        return 0;
    }

    protected ushort TyaCycle2()
    {
        return 0;
    }
    protected ushort TyaCycle1()
    {
        return 0;
    }

    protected ushort TxsCycle2()
    {
        return 0;
    }
    protected ushort TxsCycle1()
    {
        return 0;
    }

    protected ushort AneCycle2()
    {
        return 0;
    }
    protected ushort AneCycle1()
    {
        return 0;
    }

    // NOPs (illegal opcodes).
    protected ushort NopImpliedCycle2()
    {
        return 0;
    }
    protected ushort NopImpliedCycle1()
    {
        return 0;
    }

    protected ushort NopImmCycle2()
    {
        return 0;
    }
    protected ushort NopImmCycle1()
    {
        return 0;
    }

    protected ushort NopZeropageCycle3()
    {
        return 0;
    }
    protected ushort NopZeropageCycle2()
    {
        return 0;
    }
    protected ushort NopZeropageCycle1()
    {
        return 0;
    }

    protected ushort NopZeropageXCycle4()
    {
        return 0;
    }
    protected ushort NopZeropageXCycle3()
    {
        return 0;
    }
    protected ushort NopZeropageXCycle2()
    {
        return 0;
    }
    protected ushort NopZeropageXCycle1()
    {
        return 0;
    }

    protected ushort NopAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort NopAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort NopAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort NopAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort NopAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort NopAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort NopAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort NopAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort NopAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort AncCycle3()
    {
        return 0;
    }
    protected ushort AncCycle2()
    {
        return 0;
    }
    protected ushort AncCycle1()
    {
        return 0;
    }

    protected ushort ArrCycle2()
    {
        return 0;
    }
    protected ushort ArrCycle1()
    {
        return 0;
    }

    // Proper CPU instruction steps.
    protected ushort ClcCycle2()
    {
        return 0;
    }
    protected ushort ClcCycle1()
    {
        return 0;
    }

    protected ushort SeiCycle2()
    {
        return 0;
    }
    protected ushort SeiCycle1()
    {
        return 0;
    }

    protected ushort PlaCycle4()
    {
        return 0;
    }
    protected ushort PlaCycle3()
    {
        return 0;
    }
    protected ushort PlaCycle2()
    {
        return 0;
    }
    protected ushort PlaCycle1()
    {
        return 0;
    }

    protected ushort DeyCycle2()
    {
        return 0;
    }
    protected ushort DeyCycle1()
    {
        return 0;
    }

    protected ushort TxaCycle2()
    {
        return 0;
    }
    protected ushort TxaCycle1()
    {
        return 0;
    }

    protected ushort SecCycle2()
    {
        return 0;
    }
    protected ushort SecCycle1()
    {
        return 0;
    }

    protected ushort PlpCycle4()
    {
        return 0;
    }
    protected ushort PlpCycle3()
    {
        return 0;
    }
    protected ushort PlpCycle2()
    {
        return 0;
    }
    protected ushort PlpCycle1()
    {
        return 0;
    }

    protected ushort PhaCycle3()
    {
        return 0;
    }
    protected ushort PhaCycle2()
    {
        return 0;
    }
    protected ushort PhaCycle1()
    {
        return 0;
    }

    protected ushort DexCycle2()
    {
        return 0;
    }
    protected ushort DexCycle1()
    {
        return 0;
    }

    protected ushort InyCycle2()
    {
        return 0;
    }
    protected ushort InyCycle1()
    {
        return 0;
    }

    protected ushort CldCycle2()
    {
        return 0;
    }
    protected ushort CldCycle1()
    {
        return 0;
    }

    protected ushort InxCycle2()
    {
        return 0;
    }
    protected ushort InxCycle1()
    {
        return 0;
    }

    protected ushort SedCycle2()
    {
        return 0;
    }
    protected ushort SedCycle1()
    {
        return 0;
    }

    protected ushort TaxCycle2()
    {
        return 0;
    }
    protected ushort TaxCycle1()
    {
        return 0;
    }

    protected ushort TsxCycle2()
    {
        return 0;
    }
    protected ushort TsxCycle1()
    {
        return 0;
    }

    protected ushort TayCycle2()
    {
        return 0;
    }
    protected ushort TayCycle1()
    {
        return 0;
    }

    protected ushort ClvCycle2()
    {
        return 0;
    }
    protected ushort ClvCycle1()
    {
        return 0;
    }

    protected ushort CliCycle2()
    {
        return 0;
    }
    protected ushort CliCycle1()
    {
        return 0;
    }

    protected ushort PhpCycle3()
    {
        return 0;
    }
    protected ushort PhpCycle2()
    {
        return 0;
    }
    protected ushort PhpCycle1()
    {
        return 0;
    }

    protected ushort BrkCycle7()
    {
        return 0;
    }
    protected ushort BrkCycle6()
    {
        return 0;
    }
    protected ushort BrkCycle5()
    {
        return 0;
    }
    protected ushort BrkCycle4()
    {
        return 0;
    }
    protected ushort BrkCycle3()
    {
        return 0;
    }
    protected ushort BrkCycle2()
    {
        return 0;
    }
    protected ushort BrkCycle1()
    {
        return 0;
    }

    protected ushort BplCycle4()
    {
        return 0;
    }
    protected ushort BplCycle3()
    {
        return 0;
    }
    protected ushort BplCycle2()
    {
        return 0;
    }
    protected ushort BplCycle1()
    {
        return 0;
    }

    protected ushort JsrCycle6()
    {
        return 0;
    }
    protected ushort JsrCycle5()
    {
        return 0;
    }
    protected ushort JsrCycle4()
    {
        return 0;
    }
    protected ushort JsrCycle3()
    {
        return 0;
    }
    protected ushort JsrCycle2()
    {
        return 0;
    }
    protected ushort JsrCycle1()
    {
        return 0;
    }

    protected ushort BmiCycle4()
    {
        return 0;
    }
    protected ushort BmiCycle3()
    {
        return 0;
    }
    protected ushort BmiCycle2()
    {
        return 0;
    }
    protected ushort BmiCycle1()
    {
        return 0;
    }

    protected ushort RtiCycle6()
    {
        return 0;
    }
    protected ushort RtiCycle5()
    {
        return 0;
    }
    protected ushort RtiCycle4()
    {
        return 0;
    }
    protected ushort RtiCycle3()
    {
        return 0;
    }
    protected ushort RtiCycle2()
    {
        return 0;
    }
    protected ushort RtiCycle1()
    {
        return 0;
    }

    protected ushort BvcCycle4()
    {
        return 0;
    }
    protected ushort BvcCycle3()
    {
        return 0;
    }
    protected ushort BvcCycle2()
    {
        return 0;
    }
    protected ushort BvcCycle1()
    {
        return 0;
    }

    protected ushort BvsCycle4()
    {
        return 0;
    }
    protected ushort BvsCycle3()
    {
        return 0;
    }
    protected ushort BvsCycle2()
    {
        return 0;
    }
    protected ushort BvsCycle1()
    {
        return 0;
    }


    protected ushort RtsCycle6()
    {
        return 0;
    }
    protected ushort RtsCycle5()
    {
        return 0;
    }
    protected ushort RtsCycle4()
    {
        return 0;
    }
    protected ushort RtsCycle3()
    {
        return 0;
    }
    protected ushort RtsCycle2()
    {
        return 0;
    }
    protected ushort RtsCycle1()
    {
        return 0;
    }

    protected ushort BccCycle4()
    {
        return 0;
    }
    protected ushort BccCycle3()
    {
        return 0;
    }
    protected ushort BccCycle2()
    {
        return 0;
    }
    protected ushort BccCycle1()
    {
        return 0;
    }

    protected ushort LdyImmCycle2()
    {
        return 0;
    }
    protected ushort LdyImmCycle1()
    {
        return 0;
    }

    protected ushort LdyZeropageCycle3()
    {
        return 0;
    }
    protected ushort LdyZeropageCycle2()
    {
        return 0;
    }
    protected ushort LdyZeropageCycle1()
    {
        return 0;
    }

    protected ushort LdyZeropageXCycle4()
    {
        return 0;
    }
    protected ushort LdyZeropageXCycle3()
    {
        return 0;
    }
    protected ushort LdyZeropageXCycle2()
    {
        return 0;
    }
    protected ushort LdyZeropageXCycle1()
    {
        return 0;
    }

    protected ushort LdyAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort LdyAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort LdyAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort LdyAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort LdyAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort LdyAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort LdyAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort LdyAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort LdyAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort BcsCycle4()
    {
        return 0;
    }
    protected ushort BcsCycle3()
    {
        return 0;
    }
    protected ushort BcsCycle2()
    {
        return 0;
    }
    protected ushort BcsCycle1()
    {
        return 0;
    }

    protected ushort CpyImmCycle2()
    {
        return 0;
    }
    protected ushort CpyImmCycle1()
    {
        return 0;
    }

    protected ushort CpyZeropageCycle3()
    {
        return 0;
    }
    protected ushort CpyZeropageCycle2()
    {
        return 0;
    }
    protected ushort CpyZeropageCycle1()
    {
        return 0;
    }

    protected ushort CpyAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort CpyAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort CpyAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort CpyAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort BneCycle4()
    {
        return 0;
    }
    protected ushort BneCycle3()
    {
        return 0;
    }
    protected ushort BneCycle2()
    {
        return 0;
    }
    protected ushort BneCycle1()
    {
        return 0;
    }

    protected ushort CpxImmCycle2()
    {
        return 0;
    }
    protected ushort CpxImmCycle1()
    {
        return 0;
    }

    protected ushort CpxZeropageCycle3()
    {
        return 0;
    }
    protected ushort CpxZeropageCycle2()
    {
        return 0;
    }
    protected ushort CpxZeropageCycle1()
    {
        return 0;
    }

    protected ushort CpxAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort CpxAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort CpxAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort CpxAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort BeqCycle4()
    {
        return 0;
    }
    protected ushort BeqCycle3()
    {
        return 0;
    }
    protected ushort BeqCycle2()
    {
        return 0;
    }
    protected ushort BeqCycle1()
    {
        return 0;
    }

    protected ushort OraImmCycle2()
    {
        return 0;
    }

    protected ushort OraImmCycle1()
    {
        return 0;
    }

    protected ushort OraZeropageCycle3()
    {
        return 0;
    }
    protected ushort OraZeropageCycle2()
    {
        return 0;
    }
    protected ushort OraZeropageCycle1()
    {
        return 0;
    }

    protected ushort OraZeropageXCycle4()
    {
        return 0;
    }
    protected ushort OraZeropageXCycle3()
    {
        return 0;
    }
    protected ushort OraZeropageXCycle2()
    {
        return 0;
    }
    protected ushort OraZeropageXCycle1()
    {
        return 0;
    }

    protected ushort OraAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort OraAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort OraAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort OraAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort OraAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort OraAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort OraAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort OraAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort OraAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort OraAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort OraAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort OraAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort OraAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort OraAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort OraIndirectXCycle6()
    {
        return 0;
    }
    protected ushort OraIndirectXCycle5()
    {
        return 0;
    }
    protected ushort OraIndirectXCycle4()
    {
        return 0;
    }
    protected ushort OraIndirectXCycle3()
    {
        return 0;
    }
    protected ushort OraIndirectXCycle2()
    {
        return 0;
    }
    protected ushort OraIndirectXCycle1()
    {
        return 0;
    }

    protected ushort OraIndirectYCycle6()
    {
        return 0;
    }
    protected ushort OraIndirectYCycle5()
    {
        return 0;
    }
    protected ushort OraIndirectYCycle4()
    {
        return 0;
    }
    protected ushort OraIndirectYCycle3()
    {
        return 0;
    }
    protected ushort OraIndirectYCycle2()
    {
        return 0;
    }
    protected ushort OraIndirectYCycle1()
    {
        return 0;
    }


    protected ushort AndImmCycle2()
    {
        return 0;
    }
    protected ushort AndImmCycle1()
    {
        return 0;
    }

    protected ushort AndZeropageCycle3()
    {
        return 0;
    }
    protected ushort AndZeropageCycle2()
    {
        return 0;
    }
    protected ushort AndZeropageCycle1()
    {
        return 0;
    }

    protected ushort AndZeropageXCycle4()
    {
        return 0;
    }
    protected ushort AndZeropageXCycle3()
    {
        return 0;
    }
    protected ushort AndZeropageXCycle2()
    {
        return 0;
    }
    protected ushort AndZeropageXCycle1()
    {
        return 0;
    }

    protected ushort AndAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort AndAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort AndAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort AndAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort AndAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort AndAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort AndAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort AndAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort AndAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort AndAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort AndAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort AndAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort AndAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort AndAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort AndIndirectXCycle6()
    {
        return 0;
    }
    protected ushort AndIndirectXCycle5()
    {
        return 0;
    }
    protected ushort AndIndirectXCycle4()
    {
        return 0;
    }
    protected ushort AndIndirectXCycle3()
    {
        return 0;
    }
    protected ushort AndIndirectXCycle2()
    {
        return 0;
    }
    protected ushort AndIndirectXCycle1()
    {
        return 0;
    }

    protected ushort AndIndirectYCycle6()
    {
        return 0;
    }
    protected ushort AndIndirectYCycle5()
    {
        return 0;
    }
    protected ushort AndIndirectYCycle4()
    {
        return 0;
    }
    protected ushort AndIndirectYCycle3()
    {
        return 0;
    }
    protected ushort AndIndirectYCycle2()
    {
        return 0;
    }
    protected ushort AndIndirectYCycle1()
    {
        return 0;
    }

    protected ushort EorImmCycle2()
    {
        return 0;
    }
    protected ushort EorImmCycle1()
    {
        return 0;
    }

    protected ushort EorZeropageCycle3()
    {
        return 0;
    }
    protected ushort EorZeropageCycle2()
    {
        return 0;
    }
    protected ushort EorZeropageCycle1()
    {
        return 0;
    }

    protected ushort EorZeropageXCycle4()
    {
        return 0;
    }
    protected ushort EorZeropageXCycle3()
    {
        return 0;
    }
    protected ushort EorZeropageXCycle2()
    {
        return 0;
    }
    protected ushort EorZeropageXCycle1()
    {
        return 0;
    }

    protected ushort EorAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort EorAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort EorAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort EorAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort EorAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort EorAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort EorAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort EorAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort EorAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort EorAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort EorAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort EorAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort EorAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort EorAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort EorIndirectXCycle6()
    {
        return 0;
    }
    protected ushort EorIndirectXCycle5()
    {
        return 0;
    }
    protected ushort EorIndirectXCycle4()
    {
        return 0;
    }
    protected ushort EorIndirectXCycle3()
    {
        return 0;
    }
    protected ushort EorIndirectXCycle2()
    {
        return 0;
    }
    protected ushort EorIndirectXCycle1()
    {
        return 0;
    }

    protected ushort EorIndirectYCycle6()
    {
        return 0;
    }
    protected ushort EorIndirectYCycle5()
    {
        return 0;
    }
    protected ushort EorIndirectYCycle4()
    {
        return 0;
    }
    protected ushort EorIndirectYCycle3()
    {
        return 0;
    }
    protected ushort EorIndirectYCycle2()
    {
        return 0;
    }
    protected ushort EorIndirectYCycle1()
    {
        return 0;
    }

    protected ushort StaZeropageCycle3()
    {
        return 0;
    }
    protected ushort StaZeropageCycle2()
    {
        return 0;
    }
    protected ushort StaZeropageCycle1()
    {
        return 0;
    }

    protected ushort StaZeropageXCycle4()
    {
        return 0;
    }
    protected ushort StaZeropageXCycle3()
    {
        return 0;
    }
    protected ushort StaZeropageXCycle2()
    {
        return 0;
    }
    protected ushort StaZeropageXCycle1()
    {
        return 0;
    }

    protected ushort StaAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort StaAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort StaAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort StaAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort StaAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort StaAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort StaAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort StaAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort StaAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort StaAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort StaAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort StaAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort StaAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort StaAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort StaIndirectXCycle6()
    {
        return 0;
    }
    protected ushort StaIndirectXCycle5()
    {
        return 0;
    }
    protected ushort StaIndirectXCycle4()
    {
        return 0;
    }
    protected ushort StaIndirectXCycle3()
    {
        return 0;
    }
    protected ushort StaIndirectXCycle2()
    {
        return 0;
    }
    protected ushort StaIndirectXCycle1()
    {
        return 0;
    }

    protected ushort StaIndirectYCycle6()
    {
        return 0;
    }
    protected ushort StaIndirectYCycle5()
    {
        return 0;
    }
    protected ushort StaIndirectYCycle4()
    {
        return 0;
    }
    protected ushort StaIndirectYCycle3()
    {
        return 0;
    }
    protected ushort StaIndirectYCycle2()
    {
        return 0;
    }
    protected ushort StaIndirectYCycle1()
    {
        return 0;
    }

    protected ushort LdaImmCycle2()
    {
        return 0;
    }
    protected ushort LdaImmCycle1()
    {
        return 0;
    }

    protected ushort LdaZeropageCycle3()
    {
        return 0;
    }
    protected ushort LdaZeropageCycle2()
    {
        return 0;
    }
    protected ushort LdaZeropageCycle1()
    {
        return 0;
    }

    protected ushort LdaZeropageXCycle4()
    {
        return 0;
    }
    protected ushort LdaZeropageXCycle3()
    {
        return 0;
    }
    protected ushort LdaZeropageXCycle2()
    {
        return 0;
    }
    protected ushort LdaZeropageXCycle1()
    {
        return 0;
    }

    protected ushort LdaAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort LdaAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort LdaAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort LdaAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort LdaAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort LdaAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort LdaAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort LdaAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort LdaAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort LdaAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort LdaAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort LdaAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort LdaAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort LdaAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort LdaIndirectXCycle6()
    {
        return 0;
    }
    protected ushort LdaIndirectXCycle5()
    {
        return 0;
    }
    protected ushort LdaIndirectXCycle4()
    {
        return 0;
    }
    protected ushort LdaIndirectXCycle3()
    {
        return 0;
    }
    protected ushort LdaIndirectXCycle2()
    {
        return 0;
    }
    protected ushort LdaIndirectXCycle1()
    {
        return 0;
    }

    protected ushort LdaIndirectYCycle6()
    {
        return 0;
    }
    protected ushort LdaIndirectYCycle5()
    {
        return 0;
    }
    protected ushort LdaIndirectYCycle4()
    {
        return 0;
    }
    protected ushort LdaIndirectYCycle3()
    {
        return 0;
    }
    protected ushort LdaIndirectYCycle2()
    {
        return 0;
    }
    protected ushort LdaIndirectYCycle1()
    {
        return 0;
    }



    protected ushort CmpImmCycle2()
    {
        return 0;
    }
    protected ushort CmpImmCycle1()
    {
        return 0;
    }

    protected ushort CmpZeropageCycle3()
    {
        return 0;
    }
    protected ushort CmpZeropageCycle2()
    {
        return 0;
    }
    protected ushort CmpZeropageCycle1()
    {
        return 0;
    }

    protected ushort CmpZeropageXCycle4()
    {
        return 0;
    }
    protected ushort CmpZeropageXCycle3()
    {
        return 0;
    }
    protected ushort CmpZeropageXCycle2()
    {
        return 0;
    }
    protected ushort CmpZeropageXCycle1()
    {
        return 0;
    }

    protected ushort CmpAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort CmpAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort CmpAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort CmpAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort CmpAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort CmpAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort CmpAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort CmpAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort CmpAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort CmpAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort CmpAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort CmpAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort CmpAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort CmpAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort CmpIndirectXCycle6()
    {
        return 0;
    }
    protected ushort CmpIndirectXCycle5()
    {
        return 0;
    }
    protected ushort CmpIndirectXCycle4()
    {
        return 0;
    }
    protected ushort CmpIndirectXCycle3()
    {
        return 0;
    }
    protected ushort CmpIndirectXCycle2()
    {
        return 0;
    }
    protected ushort CmpIndirectXCycle1()
    {
        return 0;
    }

    protected ushort CmpIndirectYCycle6()
    {
        return 0;
    }
    protected ushort CmpIndirectYCycle5()
    {
        return 0;
    }
    protected ushort CmpIndirectYCycle4()
    {
        return 0;
    }
    protected ushort CmpIndirectYCycle3()
    {
        return 0;
    }
    protected ushort CmpIndirectYCycle2()
    {
        return 0;
    }
    protected ushort CmpIndirectYCycle1()
    {
        return 0;
    }

    protected ushort SbcImmCycle2()
    {
        return 0;
    }
    protected ushort SbcImmCycle1()
    {
        return 0;
    }

    protected ushort SbcZeropageCycle3()
    {
        return 0;
    }
    protected ushort SbcZeropageCycle2()
    {
        return 0;
    }
    protected ushort SbcZeropageCycle1()
    {
        return 0;
    }

    protected ushort SbcZeropageXCycle4()
    {
        return 0;
    }
    protected ushort SbcZeropageXCycle3()
    {
        return 0;
    }
    protected ushort SbcZeropageXCycle2()
    {
        return 0;
    }
    protected ushort SbcZeropageXCycle1()
    {
        return 0;
    }

    protected ushort SbcAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort SbcAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort SbcAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort SbcAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort SbcAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort SbcAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort SbcAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort SbcAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort SbcAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort SbcAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort SbcAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort SbcAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort SbcAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort SbcAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort SbcIndirectXCycle6()
    {
        return 0;
    }
    protected ushort SbcIndirectXCycle5()
    {
        return 0;
    }
    protected ushort SbcIndirectXCycle4()
    {
        return 0;
    }
    protected ushort SbcIndirectXCycle3()
    {
        return 0;
    }
    protected ushort SbcIndirectXCycle2()
    {
        return 0;
    }
    protected ushort SbcIndirectXCycle1()
    {
        return 0;
    }

    protected ushort SbcIndirectYCycle6()
    {
        return 0;
    }
    protected ushort SbcIndirectYCycle5()
    {
        return 0;
    }
    protected ushort SbcIndirectYCycle4()
    {
        return 0;
    }
    protected ushort SbcIndirectYCycle3()
    {
        return 0;
    }
    protected ushort SbcIndirectYCycle2()
    {
        return 0;
    }
    protected ushort SbcIndirectYCycle1()
    {
        return 0;
    }

    protected ushort LdxImmCycle2()
    {
        return 0;
    }
    protected ushort LdxImmCycle1()
    {
        return 0;
    }

    protected ushort LdxZeropageCycle3()
    {
        return 0;
    }
    protected ushort LdxZeropageCycle2()
    {
        return 0;
    }
    protected ushort LdxZeropageCycle1()
    {
        return 0;
    }

    protected ushort LdxZeropageXCycle4()
    {
        return 0;
    }
    protected ushort LdxZeropageXCycle3()
    {
        return 0;
    }
    protected ushort LdxZeropageXCycle2()
    {
        return 0;
    }
    protected ushort LdxZeropageXCycle1()
    {
        return 0;
    }

    protected ushort LdxAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort LdxAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort LdxAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort LdxAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort LdxAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort LdxAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort LdxAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort LdxAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort LdxAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort BitZeropageCycle3()
    {
        return 0;
    }
    protected ushort BitZeropageCycle2()
    {
        return 0;
    }
    protected ushort BitZeropageCycle1()
    {
        return 0;
    }

    protected ushort BitAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort BitAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort BitAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort BitAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort StyZeropageCycle3()
    {
        return 0;
    }
    protected ushort StyZeropageCycle2()
    {
        return 0;
    }
    protected ushort StyZeropageCycle1()
    {
        return 0;
    }

    protected ushort StyZeropageXCycle4()
    {
        return 0;
    }
    protected ushort StyZeropageXCycle3()
    {
        return 0;
    }
    protected ushort StyZeropageXCycle2()
    {
        return 0;
    }
    protected ushort StyZeropageXCycle1()
    {
        return 0;
    }

    protected ushort StyAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort StyAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort StyAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort StyAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort AdcImmCycle2()
    {
        return 0;
    }
    protected ushort AdcImmCycle1()
    {
        return 0;
    }

    protected ushort AdcZeropageCycle3()
    {
        return 0;
    }
    protected ushort AdcZeropageCycle2()
    {
        return 0;
    }
    protected ushort AdcZeropageCycle1()
    {
        return 0;
    }

    protected ushort AdcZeropageXCycle4()
    {
        return 0;
    }
    protected ushort AdcZeropageXCycle3()
    {
        return 0;
    }
    protected ushort AdcZeropageXCycle2()
    {
        return 0;
    }
    protected ushort AdcZeropageXCycle1()
    {
        return 0;
    }

    protected ushort AdcAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort AdcAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort AdcAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort AdcAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort AdcAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort AdcAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort AdcAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort AdcAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort AdcAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort AdcAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort AdcAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort AdcAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort AdcAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort AdcAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort AdcIndirectXCycle6()
    {
        return 0;
    }
    protected ushort AdcIndirectXCycle5()
    {
        return 0;
    }
    protected ushort AdcIndirectXCycle4()
    {
        return 0;
    }
    protected ushort AdcIndirectXCycle3()
    {
        return 0;
    }
    protected ushort AdcIndirectXCycle2()
    {
        return 0;
    }
    protected ushort AdcIndirectXCycle1()
    {
        return 0;
    }

    protected ushort AdcIndirectYCycle6()
    {
        return 0;
    }
    protected ushort AdcIndirectYCycle5()
    {
        return 0;
    }
    protected ushort AdcIndirectYCycle4()
    {
        return 0;
    }
    protected ushort AdcIndirectYCycle3()
    {
        return 0;
    }
    protected ushort AdcIndirectYCycle2()
    {
        return 0;
    }
    protected ushort AdcIndirectYCycle1()
    {
        return 0;
    }

    protected ushort AslAccumCycle2()
    {
        return 0;
    }
    protected ushort AslAccumCycle1()
    {
        return 0;
    }

    protected ushort AslZeropageCycle5()
    {
        return 0;
    }
    protected ushort AslZeropageCycle4()
    {
        return 0;
    }
    protected ushort AslZeropageCycle3()
    {
        return 0;
    }
    protected ushort AslZeropageCycle2()
    {
        return 0;
    }
    protected ushort AslZeropageCycle1()
    {
        return 0;
    }

    protected ushort AslZeropageXCycle6()
    {
        return 0;
    }
    protected ushort AslZeropageXCycle5()
    {
        return 0;
    }
    protected ushort AslZeropageXCycle4()
    {
        return 0;
    }
    protected ushort AslZeropageXCycle3()
    {
        return 0;
    }
    protected ushort AslZeropageXCycle2()
    {
        return 0;
    }
    protected ushort AslZeropageXCycle1()
    {
        return 0;
    }

    protected ushort AslAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort AslAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort AslAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort AslAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort AslAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort AslAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort AslAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort AslAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort RolAccumCycle2()
    {
        return 0;
    }
    protected ushort RolAccumCycle1()
    {
        return 0;
    }

    protected ushort RolZeropageCycle5()
    {
        return 0;
    }
    protected ushort RolZeropageCycle4()
    {
        return 0;
    }
    protected ushort RolZeropageCycle3()
    {
        return 0;
    }
    protected ushort RolZeropageCycle2()
    {
        return 0;
    }
    protected ushort RolZeropageCycle1()
    {
        return 0;
    }

    protected ushort RolZeropageXCycle6()
    {
        return 0;
    }
    protected ushort RolZeropageXCycle5()
    {
        return 0;
    }
    protected ushort RolZeropageXCycle4()
    {
        return 0;
    }
    protected ushort RolZeropageXCycle3()
    {
        return 0;
    }
    protected ushort RolZeropageXCycle2()
    {
        return 0;
    }
    protected ushort RolZeropageXCycle1()
    {
        return 0;
    }

    protected ushort RolAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort RolAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort RolAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort RolAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort RolAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort RolAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort RolAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort RolAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort RorAccumCycle2()
    {
        return 0;
    }
    protected ushort RorAccumCycle1()
    {
        return 0;
    }

    protected ushort RorZeropageCycle5()
    {
        return 0;
    }
    protected ushort RorZeropageCycle4()
    {
        return 0;
    }
    protected ushort RorZeropageCycle3()
    {
        return 0;
    }
    protected ushort RorZeropageCycle2()
    {
        return 0;
    }
    protected ushort RorZeropageCycle1()
    {
        return 0;
    }

    protected ushort RorZeropageXCycle6()
    {
        return 0;
    }
    protected ushort RorZeropageXCycle5()
    {
        return 0;
    }
    protected ushort RorZeropageXCycle4()
    {
        return 0;
    }
    protected ushort RorZeropageXCycle3()
    {
        return 0;
    }
    protected ushort RorZeropageXCycle2()
    {
        return 0;
    }
    protected ushort RorZeropageXCycle1()
    {
        return 0;
    }

    protected ushort RorAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort RorAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort RorAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort RorAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort RorAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort RorAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort RorAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort RorAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort SloAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort SloAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort SloAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort SloAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort SloAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort SloAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort SloAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort SloAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort SloAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort SloAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort SloZeropageCycle5()
    {
        return 0;
    }
    protected ushort SloZeropageCycle4()
    {
        return 0;
    }
    protected ushort SloZeropageCycle3()
    {
        return 0;
    }
    protected ushort SloZeropageCycle2()
    {
        return 0;
    }
    protected ushort SloZeropageCycle1()
    {
        return 0;
    }

    protected ushort SloIndirectXCycle8()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle7()
    {
        return 0;
    }

    protected ushort SloIndirectXCycle6()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle5()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle4()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle3()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle2()
    {
        return 0;
    }
    protected ushort SloIndirectXCycle1()
    {
        return 0;
    }

    protected ushort SloIndirectYCycle8()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle7()
    {
        return 0;
    }

    protected ushort SloIndirectYCycle6()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle5()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle4()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle3()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle2()
    {
        return 0;
    }
    protected ushort SloIndirectYCycle1()
    {
        return 0;
    }

    protected ushort RlaAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort RlaAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort RlaAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort RlaAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort RlaAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort RlaAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort RlaAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort RlaAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort RlaAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort RlaAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort RlaZeropageCycle5()
    {
        return 0;
    }
    protected ushort RlaZeropageCycle4()
    {
        return 0;
    }
    protected ushort RlaZeropageCycle3()
    {
        return 0;
    }
    protected ushort RlaZeropageCycle2()
    {
        return 0;
    }
    protected ushort RlaZeropageCycle1()
    {
        return 0;
    }

    protected ushort RlaZeropageXCycle6()
    {
        return 0;
    }
    protected ushort RlaZeropageXCycle5()
    {
        return 0;
    }
    protected ushort RlaZeropageXCycle4()
    {
        return 0;
    }
    protected ushort RlaZeropageXCycle3()
    {
        return 0;
    }
    protected ushort RlaZeropageXCycle2()
    {
        return 0;
    }
    protected ushort RlaZeropageXCycle1()
    {
        return 0;
    }

    protected ushort RlaIndirectXCycle8()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle7()
    {
        return 0;
    }

    protected ushort RlaIndirectXCycle6()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle5()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle4()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle3()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle2()
    {
        return 0;
    }
    protected ushort RlaIndirectXCycle1()
    {
        return 0;
    }

    protected ushort RlaIndirectYCycle8()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle7()
    {
        return 0;
    }

    protected ushort RlaIndirectYCycle6()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle5()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle4()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle3()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle2()
    {
        return 0;
    }
    protected ushort RlaIndirectYCycle1()
    {
        return 0;
    }

    protected ushort SreAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort SreAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort SreAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort SreAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort SreAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort SreAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort SreAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort SreAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort SreAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort SreAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort SreZeropageCycle5()
    {
        return 0;
    }
    protected ushort SreZeropageCycle4()
    {
        return 0;
    }
    protected ushort SreZeropageCycle3()
    {
        return 0;
    }
    protected ushort SreZeropageCycle2()
    {
        return 0;
    }
    protected ushort SreZeropageCycle1()
    {
        return 0;
    }

    protected ushort SreZeropageXCycle6()
    {
        return 0;
    }
    protected ushort SreZeropageXCycle5()
    {
        return 0;
    }
    protected ushort SreZeropageXCycle4()
    {
        return 0;
    }
    protected ushort SreZeropageXCycle3()
    {
        return 0;
    }
    protected ushort SreZeropageXCycle2()
    {
        return 0;
    }
    protected ushort SreZeropageXCycle1()
    {
        return 0;
    }


    protected ushort SreIndirectXCycle8()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle7()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle6()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle5()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle4()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle3()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle2()
    {
        return 0;
    }
    protected ushort SreIndirectXCycle1()
    {
        return 0;
    }

    protected ushort SreIndirectYCycle8()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle7()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle6()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle5()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle4()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle3()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle2()
    {
        return 0;
    }
    protected ushort SreIndirectYCycle1()
    {
        return 0;
    }

    protected ushort LsrAccumCycle2()
    {
        return 0;
    }
    protected ushort LsrAccumCycle1()
    {
        return 0;
    }

    protected ushort LsrZeropageCycle5()
    {
        return 0;
    }
    protected ushort LsrZeropageCycle4()
    {
        return 0;
    }
    protected ushort LsrZeropageCycle3()
    {
        return 0;
    }
    protected ushort LsrZeropageCycle2()
    {
        return 0;
    }
    protected ushort LsrZeropageCycle1()
    {
        return 0;
    }

    protected ushort LsrZeropageXCycle6()
    {
        return 0;
    }
    protected ushort LsrZeropageXCycle5()
    {
        return 0;
    }
    protected ushort LsrZeropageXCycle4()
    {
        return 0;
    }
    protected ushort LsrZeropageXCycle3()
    {
        return 0;
    }
    protected ushort LsrZeropageXCycle2()
    {
        return 0;
    }
    protected ushort LsrZeropageXCycle1()
    {
        return 0;
    }

    protected ushort LsrAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort LsrAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort LsrAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort LsrAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort LsrAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort LsrAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort LsrAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort LsrAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort RraAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort RraAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort RraAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort RraAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort RraAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort RraAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort RraAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort RraAbsoluteXCycle1()
    {
        return 0;
    }

    protected ushort RraAbsoluteYCycle7()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle6()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle5()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle4()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle3()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle2()
    {
        return 0;
    }
    protected ushort RraAbsoluteYCycle1()
    {
        return 0;
    }

    protected ushort RraZeropageCycle5()
    {
        return 0;
    }
    protected ushort RraZeropageCycle4()
    {
        return 0;
    }
    protected ushort RraZeropageCycle3()
    {
        return 0;
    }
    protected ushort RraZeropageCycle2()
    {
        return 0;
    }
    protected ushort RraZeropageCycle1()
    {
        return 0;
    }

    protected ushort RraZeropageXCycle6()
    {
        return 0;
    }
    protected ushort RraZeropageXCycle5()
    {
        return 0;
    }
    protected ushort RraZeropageXCycle4()
    {
        return 0;
    }
    protected ushort RraZeropageXCycle3()
    {
        return 0;
    }
    protected ushort RraZeropageXCycle2()
    {
        return 0;
    }
    protected ushort RraZeropageXCycle1()
    {
        return 0;
    }

    protected ushort RraIndirectXCycle8()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle7()
    {
        return 0;
    }

    protected ushort RraIndirectXCycle6()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle5()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle4()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle3()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle2()
    {
        return 0;
    }
    protected ushort RraIndirectXCycle1()
    {
        return 0;
    }

    protected ushort RraIndirectYCycle8()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle7()
    {
        return 0;
    }

    protected ushort RraIndirectYCycle6()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle5()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle4()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle3()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle2()
    {
        return 0;
    }
    protected ushort RraIndirectYCycle1()
    {
        return 0;
    }

    protected ushort JmpAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort JmpAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort JmpAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort JmpAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort JmpIndirectCycle5()
    {
        return 0;
    }
    protected ushort JmpIndirectCycle4()
    {
        return 0;
    }
    protected ushort JmpIndirectCycle3()
    {
        return 0;
    }
    protected ushort JmpIndirectCycle2()
    {
        return 0;
    }
    protected ushort JmpIndirectCycle1()
    {
        return 0;
    }

    protected ushort SaxAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort SaxAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort SaxAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort SaxAbsoluteCycle1()
    {
        return 0;
    }


    protected ushort SaxZeropageCycle3()
    {
        return 0;
    }
    protected ushort SaxZeropageCycle2()
    {
        return 0;
    }
    protected ushort SaxZeropageCycle1()
    {
        return 0;
    }

    protected ushort SaxZeropageYCycle6()
    {
        return 0;
    }
    protected ushort SaxZeropageYCycle5()
    {
        return 0;
    }
    protected ushort SaxZeropageYCycle4()
    {
        return 0;
    }
    protected ushort SaxZeropageYCycle3()
    {
        return 0;
    }
    protected ushort SaxZeropageYCycle2()
    {
        return 0;
    }
    protected ushort SaxZeropageYCycle1()
    {
        return 0;
    }

    protected ushort SaxIndirectXCycle6()
    {
        return 0;
    }
    protected ushort SaxIndirectXCycle5()
    {
        return 0;
    }
    protected ushort SaxIndirectXCycle4()
    {
        return 0;
    }
    protected ushort SaxIndirectXCycle3()
    {
        return 0;
    }
    protected ushort SaxIndirectXCycle2()
    {
        return 0;
    }
    protected ushort SaxIndirectXCycle1()
    {
        return 0;
    }

    protected ushort SaxIndirectYCycle8()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle7()
    {
        return 0;
    }

    protected ushort SaxIndirectYCycle6()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle5()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle4()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle3()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle2()
    {
        return 0;
    }
    protected ushort SaxIndirectYCycle1()
    {
        return 0;
    }

    protected ushort StxZeropageCycle3()
    {
        return 0;
    }
    protected ushort StxZeropageCycle2()
    {
        return 0;
    }
    protected ushort StxZeropageCycle1()
    {
        return 0;
    }

    protected ushort StxZeropageYCycle4()
    {
        return 0;
    }
    protected ushort StxZeropageYCycle3()
    {
        return 0;
    }
    protected ushort StxZeropageYCycle2()
    {
        return 0;
    }
    protected ushort StxZeropageYCycle1()
    {
        return 0;
    }

    protected ushort StxAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort StxAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort StxAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort StxAbsoluteCycle1()
    {
        return 0;
    }

    // DEC
    protected ushort DecZeropageCycle5()
    {
        return 0;
    }
    protected ushort DecZeropageCycle4()
    {
        return 0;
    }
    protected ushort DecZeropageCycle3()
    {
        return 0;
    }
    protected ushort DecZeropageCycle2()
    {
        return 0;
    }
    protected ushort DecZeropageCycle1()
    {
        return 0;
    }


    protected ushort DecZeropageXCycle6()
    {
        return 0;
    }
    protected ushort DecZeropageXCycle5()
    {
        return 0;
    }
    protected ushort DecZeropageXCycle4()
    {
        return 0;
    }
    protected ushort DecZeropageXCycle3()
    {
        return 0;
    }
    protected ushort DecZeropageXCycle2()
    {
        return 0;
    }
    protected ushort DecZeropageXCycle1()
    {
        return 0;
    }

    protected ushort DecAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort DecAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort DecAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort DecAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort DecAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort DecAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort DecAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort DecAbsoluteXCycle1()
    {
        return 0;
    }

    // INC
    protected ushort IncZeropageCycle5()
    {
        return 0;
    }
    protected ushort IncZeropageCycle4()
    {
        return 0;
    }
    protected ushort IncZeropageCycle3()
    {
        return 0;
    }
    protected ushort IncZeropageCycle2()
    {
        return 0;
    }
    protected ushort IncZeropageCycle1()
    {
        return 0;
    }


    protected ushort IncZeropageXCycle6()
    {
        return 0;
    }
    protected ushort IncZeropageXCycle5()
    {
        return 0;
    }
    protected ushort IncZeropageXCycle4()
    {
        return 0;
    }
    protected ushort IncZeropageXCycle3()
    {
        return 0;
    }
    protected ushort IncZeropageXCycle2()
    {
        return 0;
    }
    protected ushort IncZeropageXCycle1()
    {
        return 0;
    }

    protected ushort IncAbsoluteCycle6()
    {
        return 0;
    }
    protected ushort IncAbsoluteCycle5()
    {
        return 0;
    }
    protected ushort IncAbsoluteCycle4()
    {
        return 0;
    }
    protected ushort IncAbsoluteCycle3()
    {
        return 0;
    }
    protected ushort IncAbsoluteCycle2()
    {
        return 0;
    }
    protected ushort IncAbsoluteCycle1()
    {
        return 0;
    }

    protected ushort IncAbsoluteXCycle7()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle6()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle5()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle4()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle3()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle2()
    {
        return 0;
    }
    protected ushort IncAbsoluteXCycle1()
    {
        return 0;
    }
}