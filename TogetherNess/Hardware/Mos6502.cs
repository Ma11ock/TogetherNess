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
    
    public int CpuAdd(int a, int b, bool addCarryBit = false)
    {
        int result = a + b + (addCarryBit && CarryBit ? 1 : 0);
        
        CarryBit = result > 0xFF;
        OverflowBit = ((result ^ a) & (result ^ b) & 0x80) != 0;
        ZeroBit = result == 0;
        NegativeBit = (result & 0x80) != 0; // Negative bit is the MSb.
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
                1 => OraZeropageCycle1(),
                2 => OraZeropageCycle2(),
                3 => OraZeropageCycle3(),
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
                1 => OraAbsoluteYCycle1(),
                2 => OraAbsoluteYCycle2(),
                3 => OraAbsoluteYCycle3(),
                4 => OraAbsoluteYCycle4(),
                5 => OraAbsoluteYCycle5(),
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
                1 => AndIndirectXCycle1(),
                2 => AndIndirectXCycle2(),
                3 => AndIndirectXCycle3(),
                4 => AndIndirectXCycle4(),
                5 => AndIndirectXCycle5(),
                6 => AndIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x21)
            },       
            // 0x22 JAM (illegal)
            0x22 => Jam(),
            // 0x23 RLA X, indirect
            0x23 => CurrentInstructionStep switch
            {
                1 => RlaIndirectXCycle1(),
                2 => RlaIndirectXCycle2(),
                3 => RlaIndirectXCycle3(),
                4 => RlaIndirectXCycle4(),
                5 => RlaIndirectXCycle5(),
                6 => RlaIndirectXCycle6(),
                7 => RlaIndirectXCycle7(),
                8 => RlaIndirectXCycle8(),
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
                4 => RolZeropageCycle4(),
                5 => RolZeropageCycle5(),
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
                1 => PlpCycle1(), 
                2 => PlpCycle2(), 
                3 => PlpCycle3(), 
                4 => PlpCycle4(), 
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
                1 => AndZeropageXCycle1(), 
                2 => AndZeropageXCycle2(), 
                3 => AndZeropageXCycle3(), 
                4 => AndZeropageXCycle4(), 
                _ => throw new InvalidInstructionStepException(0x35)
            },
            // 0x36 ROL zeropage, X
            0x36 => CurrentInstructionStep switch
            {
                1 => RolZeropageXCycle1(), 
                2 => RolZeropageXCycle2(), 
                3 => RolZeropageXCycle3(), 
                4 => RolZeropageXCycle4(), 
                5 => RolZeropageXCycle5(), 
                6 => RolZeropageXCycle6(), 
                _ => throw new InvalidInstructionStepException(0x36)
            },
            // 0x37 RLA zeropage, X (illegal)
            0x37 => CurrentInstructionStep switch
            {
                1 => RlaZeropageXCycle1(), 
                2 => RlaZeropageXCycle2(), 
                3 => RlaZeropageXCycle3(), 
                4 => RlaZeropageXCycle4(), 
                5 => RlaZeropageXCycle5(), 
                6 => RlaZeropageXCycle6(), 
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
            // 0x4D EOR abs
            0x4D => CurrentInstructionStep switch
            {
                1 => EorAbsoluteCycle1(), 
                2 => EorAbsoluteCycle2(), 
                3 => EorAbsoluteCycle3(), 
                4 => EorAbsoluteCycle4(), 
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
                1 => BvcCycle1(), 
                2 => BvcCycle2(), 
                3 => BvcCycle3(), 
                4 => BvcCycle4(), 
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
                1 => SreZeropageXCycle1(), 
                2 => SreZeropageXCycle2(), 
                3 => SreZeropageXCycle3(), 
                4 => SreZeropageXCycle4(), 
                5 => SreZeropageXCycle5(), 
                6 => SreZeropageXCycle6(), 
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
                7 => SreAbsoluteYCycle7(), 
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
                3 => PlaCycle3(), 
                4 => PlaCycle4(), 
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
                1 => RorAccumCycle1(), 
                2 => RorAccumCycle2(), 
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
            // 0x77 RRA zeropage, X
            0x77 => CurrentInstructionStep switch
            {
                1 => RraZeropageXCycle1(),
                2 => RraZeropageXCycle2(),
                3 => RraZeropageXCycle3(),
                4 => RraZeropageXCycle4(),
                5 => RraZeropageXCycle5(),
                6 => RraZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0x77)
            },       
            // 0x78 SEI impl
            0x78 => CurrentInstructionStep switch
            {
                1 => SeiCycle1(),
                2 => SeiCycle2(),
                _ => throw new InvalidInstructionStepException(0x78)
            },       
            // 0x79 ADC abs, Y
            0x79 => CurrentInstructionStep switch
            {
                1 => AdcAbsoluteYCycle1(),
                2 => AdcAbsoluteYCycle2(),
                3 => AdcAbsoluteYCycle3(),
                4 => AdcAbsoluteYCycle4(),
                5 => AdcAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x79)
            },       
            // 0x7A NOP impl
            0x7A => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(),
                2 => NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0x7A)
            },       
            // 0x7B RRA abs, Y
            0x7B => CurrentInstructionStep switch
            {
                1 => RraAbsoluteYCycle1(),
                2 => RraAbsoluteYCycle2(),
                3 => RraAbsoluteYCycle3(),
                4 => RraAbsoluteYCycle4(),
                5 => RraAbsoluteYCycle5(),
                6 => RraAbsoluteYCycle6(),
                7 => RraAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0x7B)
            },       
            // 0x7C NOP abs, X
            0x7C => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(),
                2 => NopAbsoluteXCycle2(),
                3 => NopAbsoluteXCycle3(),
                4 => NopAbsoluteXCycle4(),
                5 => NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x7C)
            },       
            // 0x7D ADC abs, X
            0x7D => CurrentInstructionStep switch
            {
                1 => AdcAbsoluteXCycle1(),
                2 => AdcAbsoluteXCycle2(),
                3 => AdcAbsoluteXCycle3(),
                4 => AdcAbsoluteXCycle4(),
                5 => AdcAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x7D)
            },       
            // 0x7E ROR abs, X
            0x7E => CurrentInstructionStep switch
            {
                1 => RorAbsoluteXCycle1(),
                2 => RorAbsoluteXCycle2(),
                3 => RorAbsoluteXCycle3(),
                4 => RorAbsoluteXCycle4(),
                5 => RorAbsoluteXCycle5(),
                6 => RorAbsoluteXCycle6(),
                7 => RorAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x7E)
            },       
            // 0x7F RRA abs, X
            0x7F => CurrentInstructionStep switch
            {
                1 => RraAbsoluteXCycle1(),
                2 => RraAbsoluteXCycle2(),
                3 => RraAbsoluteXCycle3(),
                4 => RraAbsoluteXCycle4(),
                5 => RraAbsoluteXCycle5(),
                6 => RraAbsoluteXCycle6(),
                7 => RraAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0x7F)
            },       
            // 0x80 NOP imm
            0x80 => CurrentInstructionStep switch
            {
                1 => NopImmCycle1(),
                2 => NopImmCycle2(),
                _ => throw new InvalidInstructionStepException(0x80)
            },       
            // 0x81 STA X, ind
            0x81 => CurrentInstructionStep switch
            {
                1 => StaIndirectXCycle1(),
                2 => StaIndirectXCycle2(),
                3 => StaIndirectXCycle3(),
                4 => StaIndirectXCycle4(),
                5 => StaIndirectXCycle5(),
                6 => StaIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x81)
            },       
            // 0x82 NOP imm
            0x82 => CurrentInstructionStep switch
            {
                1 => NopImmCycle1(),
                2 => NopImmCycle2(),
                _ => throw new InvalidInstructionStepException(0x82)
            },       
            // 0x83 SAX x, ind
            0x83 => CurrentInstructionStep switch
            {
                1 => SaxIndirectXCycle1(),
                2 => SaxIndirectXCycle2(),
                3 => SaxIndirectXCycle3(),
                4 => SaxIndirectXCycle4(),
                5 => SaxIndirectXCycle5(),
                6 => SaxIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0x83)
            },       
            // 0x84 STY zpg
            0x84 => CurrentInstructionStep switch
            {
                1 => StyZeropageCycle1(),
                2 => StyZeropageCycle2(),
                3 => StyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x84)
            },       
            // 0x85 STA zpg
            0x85 => CurrentInstructionStep switch
            {
                1 => StaZeropageCycle1(),
                2 => StaZeropageCycle2(),
                3 => StaZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x85)
            },       
            // 0x86 STX zpg
            0x86 => CurrentInstructionStep switch
            {
                1 => StxZeropageCycle1(),
                2 => StxZeropageCycle2(),
                3 => StxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x86)
            },       
            // 0x87 SAX zpg
            0x87 => CurrentInstructionStep switch
            {
                1 => SaxZeropageCycle1(),
                2 => SaxZeropageCycle2(),
                3 => SaxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0x87)
            },       
            // 0x88 DEY impl
            0x88 => CurrentInstructionStep switch
            {
                1 => DeyCycle1(),
                2 => DeyCycle2(),
                _ => throw new InvalidInstructionStepException(0x88)
            },       
            // 0x89 DEY impl
            0x89 => CurrentInstructionStep switch
            {
                1 => NopImmCycle1(),
                2 => NopImmCycle2(),
                _ => throw new InvalidInstructionStepException(0x89)
            },       
            // 0x8A TXA impl
            0x8A => CurrentInstructionStep switch
            {
                1 => TxaCycle1(),
                2 => TxaCycle2(),
                _ => throw new InvalidInstructionStepException(0x8A)
            },       
            // 0x8B ANE impl
            0x8B => CurrentInstructionStep switch
            {
                1 => AneCycle1(),
                2 => AneCycle2(),
                _ => throw new InvalidInstructionStepException(0x8B)
            },       
            // 0x8C STY abs
            0x8C => CurrentInstructionStep switch
            {
                1 => StyAbsoluteCycle1(),
                2 => StyAbsoluteCycle2(),
                3 => StyAbsoluteCycle3(),
                4 => StyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8C)
            },       
            // 0x8D STA abs
            0x8D => CurrentInstructionStep switch
            {
                1 => StaAbsoluteCycle1(),
                2 => StaAbsoluteCycle2(),
                3 => StaAbsoluteCycle3(),
                4 => StaAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8D)
            },       
            // 0x8E STX abs
            0x8E => CurrentInstructionStep switch
            {
                1 => StxAbsoluteCycle1(),
                2 => StxAbsoluteCycle2(),
                3 => StxAbsoluteCycle3(),
                4 => StxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8E)
            },       
            // 0x8F STA abs
            0x8F => CurrentInstructionStep switch
            {
                1 => SaxAbsoluteCycle1(),
                2 => SaxAbsoluteCycle2(),
                3 => SaxAbsoluteCycle3(),
                4 => SaxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0x8F)
            },       
            // 0x90 BCC rel
            0x90 => CurrentInstructionStep switch
            {
                1 => BccCycle1(),
                2 => BccCycle2(),
                3 => BccCycle3(),
                4 => BccCycle4(),
                _ => throw new InvalidInstructionStepException(0x90)
            },       
            // 0x91 STA ind, Y
            0x91 => CurrentInstructionStep switch
            {
                1 => StaIndirectYCycle1(),
                2 => StaIndirectYCycle2(),
                3 => StaIndirectYCycle3(),
                4 => StaIndirectYCycle4(),
                5 => StaIndirectYCycle5(),
                6 => StaIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0x91)
            },       
            // 0x92 Sta ind, Y
            0x92 => Jam(),
            // 0x93 SHA ind, Y
            0x93 => CurrentInstructionStep switch
            {
                1 => ShaIndirectYCycle1(),
                2 => ShaIndirectYCycle2(),
                3 => ShaIndirectYCycle3(),
                4 => ShaIndirectYCycle4(),
                5 => ShaIndirectYCycle5(),
                _ => throw new InvalidInstructionStepException(0x93)
            },       
            // 0x94 STY zpg, X
            0x94 => CurrentInstructionStep switch
            {
                1 => StyZeropageXCycle1(),
                2 => StyZeropageXCycle2(),
                3 => StyZeropageXCycle3(),
                4 => StyZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x94)
            },       
            // 0x95 STA zpg, X
            0x95 => CurrentInstructionStep switch
            {
                1 => StaZeropageXCycle1(),
                2 => StaZeropageXCycle2(),
                3 => StaZeropageXCycle3(),
                4 => StaZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0x95)
            },       
            // 0x96 STX zpg, X
            0x96 => CurrentInstructionStep switch
            {
                1 => StxZeropageYCycle1(),
                2 => StxZeropageYCycle2(),
                3 => StxZeropageYCycle3(), 
                4 => StxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0x96)
            },       
            // 0x97 SAX zpg, Y
            0x97 => CurrentInstructionStep switch
            {
                1 => SaxZeropageYCycle1(),
                2 => SaxZeropageYCycle2(),
                3 => SaxZeropageYCycle3(),
                4 => SaxZeropageYCycle4(),
                5 => SaxZeropageYCycle5(),
                6 => SaxZeropageYCycle6(),
                _ => throw new InvalidInstructionStepException(0x97)
            },       
            // 0x98 TYA impl
            0x98 => CurrentInstructionStep switch
            {
                1 => TyaCycle1(),
                2 => TyaCycle2(),
                _ => throw new InvalidInstructionStepException(0x98)
            },       
            // 0x99 STA abs, Y
            0x99 => CurrentInstructionStep switch
            {
                1 => StaAbsoluteYCycle1(),
                2 => StaAbsoluteYCycle2(),
                3 => StaAbsoluteYCycle3(),
                4 => StaAbsoluteYCycle4(),
                5 => StaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x99)
            },       
            // 0x9A TXS impl
            0x9A => CurrentInstructionStep switch
            {
                1 => TxsCycle1(),
                2 => TxsCycle2(),
                _ => throw new InvalidInstructionStepException(0x9A)
            },       
            // 0x9B TAS abs, Y
            0x9B => CurrentInstructionStep switch
            {
                1 => TasCycle1(),
                2 => TasCycle2(),
                3 => TasCycle3(),
                4 => TasCycle4(),
                5 => TasCycle5(),
                _ => throw new InvalidInstructionStepException(0x9B)
            },       
            // 0x9C SHY abs, X
            0x9C => CurrentInstructionStep switch
            {
                1 => ShyCycle1(),
                2 => ShyCycle2(),
                3 => ShyCycle3(),
                4 => ShyCycle4(),
                5 => ShyCycle5(),
                _ => throw new InvalidInstructionStepException(0x9C)
            },       
            // 0x9D STA abs, X
            0x9D => CurrentInstructionStep switch
            {
                1 => StaAbsoluteXCycle1(),
                2 => StaAbsoluteXCycle2(),
                3 => StaAbsoluteXCycle3(),
                4 => StaAbsoluteXCycle4(),
                5 => StaAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0x9D)
            },       
            // 0x9E SHX abs, Y
            0x9E => CurrentInstructionStep switch
            {
                1 => ShxCycle1(),
                2 => ShxCycle2(),
                3 => ShxCycle3(),
                4 => ShxCycle4(),
                5 => ShxCycle5(),
                _ => throw new InvalidInstructionStepException(0x9E)
            },       
            // 0x9F SHX abs, Y
            0x9F => CurrentInstructionStep switch
            {
                1 => ShaAbsoluteYCycle1(),
                2 => ShaAbsoluteYCycle2(),
                3 => ShaAbsoluteYCycle3(),
                4 => ShaAbsoluteYCycle4(),
                5 => ShaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0x9F)
            },       
            // 0xA0 LDY imm
            0xA0 => CurrentInstructionStep switch
            {
                1 => LdyImmCycle1(),
                2 => LdyImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xA0)
            },       
            // 0xA1 LDA X, ind
            0xA1 => CurrentInstructionStep switch
            {
                1 => LdaIndirectXCycle1(),
                2 => LdaIndirectXCycle2(),
                3 => LdaIndirectXCycle3(),
                4 => LdaIndirectXCycle4(),
                5 => LdaIndirectXCycle5(),
                6 => LdaIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xA1)
            },       
            // 0xA2 LDX imm
            0xA2 => CurrentInstructionStep switch
            {
                1 => LdxImmCycle1(),
                2 => LdxImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xA2)
            },       
            // 0xA3 LAX X, ind
            0xA3 => CurrentInstructionStep switch
            {
                1 => LaxIndirectXCycle1(),
                2 => LaxIndirectXCycle2(),
                3 => LaxIndirectXCycle3(),
                4 => LaxIndirectXCycle4(),
                5 => LaxIndirectXCycle5(),
                6 => LaxIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xA3)
            },       
            // 0xA4 LDY zpg
            0xA4 => CurrentInstructionStep switch
            {
                1 => LdyZeropageCycle1(),
                2 => LdyZeropageCycle2(),
                3 => LdyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA4)
            },       
            // 0xA5 LDA zpg
            0xA5 => CurrentInstructionStep switch
            {
                1 => LdaZeropageCycle1(),
                2 => LdaZeropageCycle2(),
                3 => LdaZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA5)
            },       
            // 0xA6 LDX zpg
            0xA6 => CurrentInstructionStep switch
            {
                1 => LdxZeropageCycle1(),
                2 => LdxZeropageCycle2(),
                3 => LdxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA6)
            },       
            // 0xA7 LAX zpg
            0xA7 => CurrentInstructionStep switch
            {
                1 => LaxZeropageCycle1(),
                2 => LaxZeropageCycle2(),
                3 => LaxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xA7)
            },       
            // 0xA8 TAY impl
            0xA8 => CurrentInstructionStep switch
            {
                1 => TayCycle1(),
                2 => TayCycle2(),
                _ => throw new InvalidInstructionStepException(0xA8)
            },       
            // 0xA9 LDA imm
            0xA9 => CurrentInstructionStep switch
            {
                1 => LdaImmCycle1(),
                2 => LdaImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xA9)
            },       
            // 0xAA TAX impl
            0xAA => CurrentInstructionStep switch
            {
                1 => TaxCycle1(),
                2 => TaxCycle2(),
                _ => throw new InvalidInstructionStepException(0xAA)
            },       
            // 0xAB LXA imm
            0xAB => CurrentInstructionStep switch
            {
                1 => LxaCycle1(),
                2 => LxaCycle2(),
                _ => throw new InvalidInstructionStepException(0xAB)
            },       
            // 0xAC LDY abs
            0xAC => CurrentInstructionStep switch
            {
                1 => LdyAbsoluteCycle1(),
                2 => LdyAbsoluteCycle2(),
                3 => LdyAbsoluteCycle3(),
                4 => LdyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAC)
            },       
            // 0xAD LDA abs
            0xAD => CurrentInstructionStep switch
            {
                1 => LdaAbsoluteCycle1(),
                2 => LdaAbsoluteCycle2(),
                3 => LdaAbsoluteCycle3(),
                4 => LdaAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAD)
            },       
            // 0xAE LDX abs
            0xAE => CurrentInstructionStep switch
            {
                1 => LdxAbsoluteCycle1(),
                2 => LdxAbsoluteCycle2(),
                3 => LdxAbsoluteCycle3(),
                4 => LdxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAE)
            },       
            // 0xAF LAX abs
            0xAF => CurrentInstructionStep switch
            {
                1 => LaxAbsoluteCycle1(),
                2 => LaxAbsoluteCycle2(),
                3 => LaxAbsoluteCycle3(),
                4 => LaxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xAF)
            },       
            // 0xB0 BCS rel
            0xB0 => CurrentInstructionStep switch
            {
                1 => BcsCycle1(),
                2 => BcsCycle2(),
                3 => BcsCycle3(),
                4 => BcsCycle4(),
                _ => throw new InvalidInstructionStepException(0xB0)
            },       
            // 0xB1 LDA ind, Y
            0xB1 => CurrentInstructionStep switch
            {
                1 => LdaIndirectYCycle1(),
                2 => LdaIndirectYCycle2(),
                3 => LdaIndirectYCycle3(),
                4 => LdaIndirectYCycle4(),
                5 => LdaIndirectYCycle5(),
                6 => LdaIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xB1)
            },       
            // 0xB2 Jam
            0xB2 => Jam(),
            // 0xB3 LAX ind, Y
            0xB3 => CurrentInstructionStep switch
            {
                1 => LaxIndirectYCycle1(),
                2 => LaxIndirectYCycle2(),
                3 => LaxIndirectYCycle3(),
                4 => LaxIndirectYCycle4(),
                5 => LaxIndirectYCycle5(),
                6 => LaxIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xB3)
            },       
            // 0xB4 LDY zpg, X
            0xB4 => CurrentInstructionStep switch
            {
                1 => LdyZeropageXCycle1(),
                2 => LdyZeropageXCycle2(),
                3 => LdyZeropageXCycle3(),
                4 => LdyZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xB4)
            },       
            // 0xB5 LDA zpg, X
            0xB5 => CurrentInstructionStep switch
            {
                1 => LdaZeropageXCycle1(),
                2 => LdaZeropageXCycle2(),
                3 => LdaZeropageXCycle3(),
                4 => LdaZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xB5)
            },       
            // 0xB6 LDX zpg, Y
            0xB6 => CurrentInstructionStep switch
            {
                1 => LdxZeropageYCycle1(),
                2 => LdxZeropageYCycle2(),
                3 => LdxZeropageYCycle3(),
                4 => LdxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0xB6)
            },       
            // 0xB7 LAX zpg, X
            0xB7 => CurrentInstructionStep switch
            {
                1 => LaxZeropageYCycle1(),
                2 => LaxZeropageYCycle2(),
                3 => LaxZeropageYCycle3(),
                4 => LaxZeropageYCycle4(),
                _ => throw new InvalidInstructionStepException(0xB7)
            },       
            // 0xB8 CLV impl
            0xB8 => CurrentInstructionStep switch
            {
                1 => ClvCycle1(),
                2 => ClvCycle2(),
                _ => throw new InvalidInstructionStepException(0xB8)
            },       
            // 0xB9 LDA abs, Y
            0xB9 => CurrentInstructionStep switch
            {
                1 => LdaAbsoluteYCycle1(),
                2 => LdaAbsoluteYCycle2(),
                3 => LdaAbsoluteYCycle3(),
                4 => LdaAbsoluteYCycle4(),
                5 => LdaAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xB9)
            },       
            // 0xBA TSX impl
            0xBA => CurrentInstructionStep switch
            {
                1 => TsxCycle1(),
                2 => TsxCycle2(),
                _ => throw new InvalidInstructionStepException(0xBA)
            },       
            // 0xBB LAS abs, Y
            0xBB => CurrentInstructionStep switch
            {
                1 => LasCycle1(),
                2 => LasCycle2(),
                3 => LasCycle3(),
                4 => LasCycle4(),
                _ => throw new InvalidInstructionStepException(0xBB)
            },       
            // 0xBC LDY abs, X
            0xBC => CurrentInstructionStep switch
            {
                1 => LdyAbsoluteXCycle1(),
                2 => LdyAbsoluteXCycle2(),
                3 => LdyAbsoluteXCycle3(),
                4 => LdyAbsoluteXCycle4(),
                5 => LdyAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xBC)
            },       
            // 0xBD LDA abs, X
            0xBD => CurrentInstructionStep switch
            {
                1 => LdaAbsoluteXCycle1(),
                2 => LdaAbsoluteXCycle2(),
                3 => LdaAbsoluteXCycle3(),
                4 => LdaAbsoluteXCycle4(),
                5 => LdaAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xBD)
            },       
            // 0xBE LDX abs, Y
            0xBE => CurrentInstructionStep switch
            {
                1 => LdxAbsoluteYCycle1(),
                2 => LdxAbsoluteYCycle2(),
                3 => LdxAbsoluteYCycle3(),
                4 => LdxAbsoluteYCycle4(),
                5 => LdxAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xBE)
            },       
            // 0xBF LAX abs, Y
            0xBF => CurrentInstructionStep switch
            {
                1 => LaxAbsoluteYCycle1(),
                2 => LaxAbsoluteYCycle2(),
                3 => LaxAbsoluteYCycle3(),
                4 => LaxAbsoluteYCycle4(),
                5 => LaxAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xBF)
            },       
            // 0xC0 CPY imm
            0xC0 => CurrentInstructionStep switch
            {
                1 => CpyImmCycle1(),
                2 => CpyImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xC0)
            },       
            // 0xC1 CMP X, ind
            0xC1 => CurrentInstructionStep switch
            {
                1 => CmpIndirectXCycle1(),
                2 => CmpIndirectXCycle2(),
                3 => CmpIndirectXCycle3(),
                4 => CmpIndirectXCycle4(),
                5 => CmpIndirectXCycle5(),
                6 => CmpIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xC1)
            },       
            // 0xC2 NOP
            0xC2 => Jam(),
            // 0xC3 DCP X, ind
            0xC3 => CurrentInstructionStep switch
            {
                1 => DcpIndirectXCycle1(),
                2 => DcpIndirectXCycle2(),
                3 => DcpIndirectXCycle3(),
                4 => DcpIndirectXCycle4(),
                5 => DcpIndirectXCycle5(),
                6 => DcpIndirectXCycle6(),
                7 => DcpIndirectXCycle7(),
                8 => DcpIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0xC3)
            },       
            // 0xC4 CPY zpg
            0xC4 => CurrentInstructionStep switch
            {
                1 => CpyZeropageCycle1(),
                2 => CpyZeropageCycle2(),
                3 => CpyZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xC4)
            },       
            // 0xC5 CMP zpg
            0xC5 => CurrentInstructionStep switch
            {
                1 => CmpZeropageCycle1(),
                2 => CmpZeropageCycle2(),
                3 => CmpZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xC5)
            },       
            // 0xC6 DEC zpg
            0xC6 => CurrentInstructionStep switch
            {
                1 => DecZeropageCycle1(),
                2 => DecZeropageCycle2(),
                3 => DecZeropageCycle3(),
                4 => DecZeropageCycle4(),
                5 => DecZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0xC6)
            },       
            // 0xC7 DCP zpg
            0xC7 => CurrentInstructionStep switch
            {
                1 => DcpZeropageCycle1(),
                2 => DcpZeropageCycle2(),
                3 => DcpZeropageCycle3(),
                4 => DcpZeropageCycle4(),
                5 => DcpZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0xC7)
            },       
            // 0xC8 INY impl
            0xC8 => CurrentInstructionStep switch
            {
                1 => InyCycle1(),
                2 => InyCycle2(),
                _ => throw new InvalidInstructionStepException(0xC8)
            },       
            // 0xC9 CMP imm
            0xC9 => CurrentInstructionStep switch
            {
                1 => CmpImmCycle1(),
                2 => CmpImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xC9)
            },       
            // 0xCA DEX impl
            0xCA => CurrentInstructionStep switch
            {
                1 => DexCycle1(),
                2 => DexCycle2(),
                _ => throw new InvalidInstructionStepException(0xCA)
            },       
            // 0xCB SBX imm
            0xCB => CurrentInstructionStep switch
            {
                1 => SbxCycle1(),
                2 => SbxCycle2(),
                _ => throw new InvalidInstructionStepException(0xCB)
            },       
            // 0xCC CPY abs
            0xCC => CurrentInstructionStep switch
            {
                1 => CpyAbsoluteCycle1(),
                2 => CpyAbsoluteCycle2(),
                3 => CpyAbsoluteCycle3(),
                4 => CpyAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xCC)
            },       
            // 0xCD CMP abs
            0xCD => CurrentInstructionStep switch
            {
                1 => CmpAbsoluteCycle1(),
                2 => CmpAbsoluteCycle2(),
                3 => CmpAbsoluteCycle3(),
                4 => CmpAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xCD)
            },       
            // 0xCE DEC abs
            0xCE => CurrentInstructionStep switch
            {
                1 => DecAbsoluteCycle1(),
                2 => DecAbsoluteCycle2(),
                3 => DecAbsoluteCycle3(),
                4 => DecAbsoluteCycle4(),
                5 => DecAbsoluteCycle5(),
                6 => DecAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xCE)
            },       
            // 0xCF DCP abs
            0xCF => CurrentInstructionStep switch
            {
                1 => DcpAbsoluteCycle1(),
                2 => DcpAbsoluteCycle2(),
                3 => DcpAbsoluteCycle3(),
                4 => DcpAbsoluteCycle4(),
                5 => DcpAbsoluteCycle5(),
                6 => DcpAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xCF)
            },       
            // 0xD0 BNE rel
            0xD0 => CurrentInstructionStep switch
            {
                1 => BneCycle1(),
                2 => BneCycle2(),
                3 => BneCycle3(),
                4 => BneCycle4(),
                _ => throw new InvalidInstructionStepException(0xD0)
            },       
            // 0xD1 CMP ind, Y
            0xD1 => CurrentInstructionStep switch
            {
                1 => CmpIndirectYCycle1(),
                2 => CmpIndirectYCycle2(),
                3 => CmpIndirectYCycle3(),
                4 => CmpIndirectYCycle4(),
                5 => CmpIndirectYCycle5(),
                6 => CmpIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xD1)
            },       
            // 0xD2 Jam
            0xD2 => Jam(),
            // 0xD3 DCP ind, Y
            0xD3 => CurrentInstructionStep switch
            {
                1 => DcpIndirectYCycle1(),
                2 => DcpIndirectYCycle2(),
                3 => DcpIndirectYCycle3(),
                4 => DcpIndirectYCycle4(),
                5 => DcpIndirectYCycle5(),
                6 => DcpIndirectYCycle6(),
                7 => DcpIndirectYCycle7(),
                8 => DcpIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0xD3)
            },       
            // 0xD4 NOP zpg, X
            0xD4 => CurrentInstructionStep switch
            {
                1 => NopZeropageXCycle1(),
                2 => NopZeropageXCycle2(),
                3 => NopZeropageXCycle3(),
                4 => NopZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xD4)
            },       
            // 0xD5 CMP zpg, X
            0xD5 => CurrentInstructionStep switch
            {
                1 => CmpZeropageXCycle1(),
                2 => CmpZeropageXCycle2(),
                3 => CmpZeropageXCycle3(),
                4 => CmpZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xD5)
            },       
            // 0xD6 DEC zpg, X
            0xD6 => CurrentInstructionStep switch
            {
                1 => DecZeropageXCycle1(),
                2 => DecZeropageXCycle2(),
                3 => DecZeropageXCycle3(),
                4 => DecZeropageXCycle4(),
                5 => DecZeropageXCycle5(),
                6 => DecZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xD6)
            },       
            // 0xD7 DCP zpg, X
            0xD7 => CurrentInstructionStep switch
            {
                1 => DcpZeropageXCycle1(),
                2 => DcpZeropageXCycle2(),
                3 => DcpZeropageXCycle3(),
                4 => DcpZeropageXCycle4(),
                5 => DcpZeropageXCycle5(),
                6 => DcpZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xD7)
            },       
            // 0xD8 CLD impl
            0xD8 => CurrentInstructionStep switch
            {
                1 => CldCycle1(),
                2 => CldCycle2(),
                _ => throw new InvalidInstructionStepException(0xD8)
            },       
            // 0xD9 CMP abs, Y
            0xD9 => CurrentInstructionStep switch
            {
                1 => CmpAbsoluteYCycle1(),
                2 => CmpAbsoluteYCycle2(),
                3 => CmpAbsoluteYCycle3(),
                4 => CmpAbsoluteYCycle4(),
                5 => CmpAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xD9)
            },       
            // 0xDA NOP impl
            0xDA => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(),
                2 => NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xDA)
            },       
            // 0xDB DCP abs, Y
            0xDB => CurrentInstructionStep switch
            {
                1 => DcpAbsoluteYCycle1(),
                2 => DcpAbsoluteYCycle2(),
                3 => DcpAbsoluteYCycle3(),
                4 => DcpAbsoluteYCycle4(),
                5 => DcpAbsoluteYCycle5(),
                6 => DcpAbsoluteYCycle6(),
                7 => DcpAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0xDB)
            },       
            // 0xDC NOP abs, X
            0xDC => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(),
                2 => NopAbsoluteXCycle2(),
                3 => NopAbsoluteXCycle3(),
                4 => NopAbsoluteXCycle4(),
                5 => NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xDC)
            },       
            // 0xDD CMP abs, X
            0xDD => CurrentInstructionStep switch
            {
                1 => CmpAbsoluteXCycle1(),
                2 => CmpAbsoluteXCycle2(),
                3 => CmpAbsoluteXCycle3(),
                4 => CmpAbsoluteXCycle4(),
                5 => CmpAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xDD)
            },
            // 0xDE DEC abs, X
            0xDE => CurrentInstructionStep switch
            {
                1 => DecAbsoluteXCycle1(),
                2 => DecAbsoluteXCycle2(),
                3 => DecAbsoluteXCycle3(),
                4 => DecAbsoluteXCycle4(),
                5 => DecAbsoluteXCycle5(),
                6 => DecAbsoluteXCycle6(),
                7 => DecAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xDE)
            },
            // 0xDF DCP abs, X
            0xDF => CurrentInstructionStep switch
            {
                1 => DcpAbsoluteXCycle1(),
                2 => DcpAbsoluteXCycle2(),
                3 => DcpAbsoluteXCycle3(),
                4 => DcpAbsoluteXCycle4(),
                5 => DcpAbsoluteXCycle5(),
                6 => DcpAbsoluteXCycle6(),
                7 => DcpAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xDF)
            },
            // 0xE0 CPX imm
            0xE0 => CurrentInstructionStep switch
            {
                1 => CpxImmCycle1(),
                2 => CpxImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xE0)
            },
            // 0xE1 SBC X, ind
            0xE1 => CurrentInstructionStep switch
            {
                1 => SbcIndirectXCycle1(),
                2 => SbcIndirectXCycle2(),
                3 => SbcIndirectXCycle3(),
                4 => SbcIndirectXCycle4(),
                5 => SbcIndirectXCycle5(),
                6 => SbcIndirectXCycle6(),
                _ => throw new InvalidInstructionStepException(0xE1)
            },
            // 0xE2 NOP imm
            0xE2 => CurrentInstructionStep switch
            {
                1 => NopImmCycle1(),
                2 => NopImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xE2)
            },
            // 0xE3 ISC x, ind
            0xE3 => CurrentInstructionStep switch
            {
                1 => IscIndirectXCycle1(),
                2 => IscIndirectXCycle2(),
                3 => IscIndirectXCycle3(),
                4 => IscIndirectXCycle4(),
                5 => IscIndirectXCycle5(),
                6 => IscIndirectXCycle6(),
                7 => IscIndirectXCycle7(),
                8 => IscIndirectXCycle8(),
                _ => throw new InvalidInstructionStepException(0xE3)
            },
            // 0xE4 CPX zpg
            0xE4 => CurrentInstructionStep switch
            {
                1 => CpxZeropageCycle1(),
                2 => CpxZeropageCycle2(),
                3 => CpxZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xE4)
            },
            // 0xE5 SBC zpg
            0xE5 => CurrentInstructionStep switch
            {
                1 => SbcZeropageCycle1(),
                2 => SbcZeropageCycle2(),
                3 => SbcZeropageCycle3(),
                _ => throw new InvalidInstructionStepException(0xE5)
            },
            // 0xE6 INC zpg
            0xE6 => CurrentInstructionStep switch
            {
                1 => IncZeropageCycle1(),
                2 => IncZeropageCycle2(),
                3 => IncZeropageCycle3(),
                4 => IncZeropageCycle4(),
                5 => IncZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0xE6)
            },
            // 0xE7 ISC zpg
            0xE7 => CurrentInstructionStep switch
            {
                1 => IscZeropageCycle1(),
                2 => IscZeropageCycle2(),
                3 => IscZeropageCycle3(),
                4 => IscZeropageCycle4(),
                5 => IscZeropageCycle5(),
                _ => throw new InvalidInstructionStepException(0xE7)
            },
            // 0xE8 INX impl
            0xE8 => CurrentInstructionStep switch
            {
                1 => InxCycle1(),
                2 => InxCycle2(),
                _ => throw new InvalidInstructionStepException(0xE8)
            },
            // 0xE9 SBC imm
            0xE9 => CurrentInstructionStep switch
            {
                1 => SbcImmCycle1(),
                2 => SbcImmCycle2(),
                _ => throw new InvalidInstructionStepException(0xE9)
            },
            // 0xEA NOP impl
            0xEA => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(),
                2 => NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xEA)
            },
            // 0xEB USBC imm
            0xEB => CurrentInstructionStep switch
            {
                1 => UsbcCycle1(),
                2 => UsbcCycle2(),
                _ => throw new InvalidInstructionStepException(0xEB)
            },
            // 0xEC CPX abs
            0xEC => CurrentInstructionStep switch
            {
                1 => CpxAbsoluteCycle1(),
                2 => CpxAbsoluteCycle2(),
                3 => CpxAbsoluteCycle3(),
                4 => CpxAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xEC)
            },
            // 0xED SBC abs
            0xED => CurrentInstructionStep switch
            {
                1 => SbcAbsoluteCycle1(),
                2 => SbcAbsoluteCycle2(),
                3 => SbcAbsoluteCycle3(),
                4 => SbcAbsoluteCycle4(),
                _ => throw new InvalidInstructionStepException(0xED)
            },
            // 0xEE INC abs
            0xEE => CurrentInstructionStep switch
            {
                1 => IncAbsoluteCycle1(),
                2 => IncAbsoluteCycle2(),
                3 => IncAbsoluteCycle3(),
                4 => IncAbsoluteCycle4(),
                5 => IncAbsoluteCycle5(),
                6 => IncAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xEE)
            },
            // 0xEF ISC abs
            0xEF => CurrentInstructionStep switch
            {
                1 => IscAbsoluteCycle1(),
                2 => IscAbsoluteCycle2(),
                3 => IscAbsoluteCycle3(),
                4 => IscAbsoluteCycle4(),
                5 => IscAbsoluteCycle5(),
                6 => IscAbsoluteCycle6(),
                _ => throw new InvalidInstructionStepException(0xEF)
            },
            // 0xF0 BEQ rel
            0xF0 => CurrentInstructionStep switch
            {
                1 => BeqCycle1(),
                2 => BeqCycle2(),
                3 => BeqCycle3(),
                4 => BeqCycle4(),
                _ => throw new InvalidInstructionStepException(0xF0)
            },
            // 0xF1 SBC ind, Y
            0xF1 => CurrentInstructionStep switch
            {
                1 => SbcIndirectYCycle1(),
                2 => SbcIndirectYCycle2(),
                3 => SbcIndirectYCycle3(),
                4 => SbcIndirectYCycle4(),
                5 => SbcIndirectYCycle5(),
                6 => SbcIndirectYCycle6(),
                _ => throw new InvalidInstructionStepException(0xF1)
            },
            // 0xF2 JAM
            0xF2 => Jam(),
            // 0xF3 ISC ind, Y
            0xF3 => CurrentInstructionStep switch
            {
                1 => IscIndirectYCycle1(),
                2 => IscIndirectYCycle2(),
                3 => IscIndirectYCycle3(),
                4 => IscIndirectYCycle4(),
                5 => IscIndirectYCycle5(),
                6 => IscIndirectYCycle6(),
                7 => IscIndirectYCycle7(),
                8 => IscIndirectYCycle8(),
                _ => throw new InvalidInstructionStepException(0xF3)
            },
            // 0xF4 NOP zpg, X
            0xF4 => CurrentInstructionStep switch
            {
                1 => NopZeropageXCycle1(),
                2 => NopZeropageXCycle2(),
                3 => NopZeropageXCycle3(),
                4 => NopZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xF4)
            },
            // 0xF5 SBC zpg, X
            0xF5 => CurrentInstructionStep switch
            {
                1 => SbcZeropageXCycle1(),
                2 => SbcZeropageXCycle2(),
                3 => SbcZeropageXCycle3(),
                4 => SbcZeropageXCycle4(),
                _ => throw new InvalidInstructionStepException(0xF5)
            },
            // 0xF6 INC zpg, X
            0xF6 => CurrentInstructionStep switch
            {
                1 => IncZeropageXCycle1(),
                2 => IncZeropageXCycle2(),
                3 => IncZeropageXCycle3(),
                4 => IncZeropageXCycle4(),
                5 => IncZeropageXCycle5(),
                6 => IncZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xF6)
            },
            // 0xF7 ISC zpg, X
            0xF7 => CurrentInstructionStep switch
            {
                1 => IscZeropageXCycle1(),
                2 => IscZeropageXCycle2(),
                3 => IscZeropageXCycle3(),
                4 => IscZeropageXCycle4(),
                5 => IscZeropageXCycle5(),
                6 => IscZeropageXCycle6(),
                _ => throw new InvalidInstructionStepException(0xF7)
            },
            // 0xF8 SED impl
            0xF8 => CurrentInstructionStep switch
            {
                1 => SedCycle1(),
                2 => SedCycle2(),
                _ => throw new InvalidInstructionStepException(0xF8)
            },
            // 0xF9 SBC abs, Y
            0xF9 => CurrentInstructionStep switch
            {
                1 => SbcAbsoluteYCycle1(),
                2 => SbcAbsoluteYCycle2(),
                3 => SbcAbsoluteYCycle3(),
                4 => SbcAbsoluteYCycle4(),
                5 => SbcAbsoluteYCycle5(),
                _ => throw new InvalidInstructionStepException(0xF9)
            },
            // 0xFA NOP impl
            0xFA => CurrentInstructionStep switch
            {
                1 => NopImpliedCycle1(),
                2 => NopImpliedCycle2(),
                _ => throw new InvalidInstructionStepException(0xFA)
            },
            // 0xFB ISC abs, Y
            0xFB => CurrentInstructionStep switch
            {
                1 => IscAbsoluteYCycle1(),
                2 => IscAbsoluteYCycle2(),
                3 => IscAbsoluteYCycle3(),
                4 => IscAbsoluteYCycle4(),
                5 => IscAbsoluteYCycle5(),
                6 => IscAbsoluteYCycle6(),
                7 => IscAbsoluteYCycle7(),
                _ => throw new InvalidInstructionStepException(0xFB)
            },
            // 0xFC NOP abs, X
            0xFC => CurrentInstructionStep switch
            {
                1 => NopAbsoluteXCycle1(),
                2 => NopAbsoluteXCycle2(),
                3 => NopAbsoluteXCycle3(),
                4 => NopAbsoluteXCycle4(),
                5 => NopAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xFC)
            },
            // 0xFD SBC abs, X
            0xFD => CurrentInstructionStep switch
            {
                1 => SbcAbsoluteXCycle1(),
                2 => SbcAbsoluteXCycle2(),
                3 => SbcAbsoluteXCycle3(),
                4 => SbcAbsoluteXCycle4(),
                5 => SbcAbsoluteXCycle5(),
                _ => throw new InvalidInstructionStepException(0xFD)
            },
            // 0xFE INC abs, X
            0xFE => CurrentInstructionStep switch
            {
                1 => IncAbsoluteXCycle1(),
                2 => IncAbsoluteXCycle2(),
                3 => IncAbsoluteXCycle3(),
                4 => IncAbsoluteXCycle4(),
                5 => IncAbsoluteXCycle5(),
                6 => IncAbsoluteXCycle6(),
                7 => IncAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xFE)
            },
            // 0xFF ISC abs, X
            0xFF => CurrentInstructionStep switch
            {
                1 => IscAbsoluteXCycle1(),
                2 => IscAbsoluteXCycle2(),
                3 => IscAbsoluteXCycle3(),
                4 => IscAbsoluteXCycle4(),
                5 => IscAbsoluteXCycle5(),
                6 => IscAbsoluteXCycle6(),
                7 => IscAbsoluteXCycle7(),
                _ => throw new InvalidInstructionStepException(0xFF)
            },
        };

    private ushort AlrCycle2()
    {
        return 0;
    }

    private ushort AlrCycle1()
    {
        return 0;
    }

    // usbc
    private ushort UsbcCycle2()
    {
        return 0;
    }

    private ushort UsbcCycle1()
    {
        return 0;
    }

    // sbx
    private ushort SbxCycle2()
    {
        return 0;
    }

    private ushort SbxCycle1()
    {
        return 0;
    }

    // isc
    private ushort IscZeropageCycle5()
    {
        return 0;
    }

    private ushort IscZeropageCycle4()
    {
        return 0;
    }

    private ushort IscZeropageCycle3()
    {
        return 0;
    }

    private ushort IscZeropageCycle2()
    {
        return 0;
    }
    private ushort IscZeropageCycle1()
    {
        return 0;
    }


    private ushort IscZeropageXCycle6()
    {
        return 0;
    }
    private ushort IscZeropageXCycle5()
    {
        return 0;
    }
    private ushort IscZeropageXCycle4()
    {
        return 0;
    }
    private ushort IscZeropageXCycle3()
    {
        return 0;
    }
    private ushort IscZeropageXCycle2()
    {
        return 0;
    }
    private ushort IscZeropageXCycle1()
    {
        return 0;
    }

    private ushort IscAbsoluteCycle6()
    {
        return 0;
    }
    private ushort IscAbsoluteCycle5()
    {
        return 0;
    }
    private ushort IscAbsoluteCycle4()
    {
        return 0;
    }
    private ushort IscAbsoluteCycle3()
    {
        return 0;
    }
    private ushort IscAbsoluteCycle2()
    {
        return 0;
    }
    private ushort IscAbsoluteCycle1()
    {
        return 0;
    }

    private ushort IscAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort IscAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort IscAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort IscAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort IscIndirectXCycle8()
    {
        return 0;
    }
    private ushort IscIndirectXCycle7()
    {
        return 0;
    }
    private ushort IscIndirectXCycle6()
    {
        return 0;
    }
    private ushort IscIndirectXCycle5()
    {
        return 0;
    }
    private ushort IscIndirectXCycle4()
    {
        return 0;
    }
    private ushort IscIndirectXCycle3()
    {
        return 0;
    }
    private ushort IscIndirectXCycle2()
    {
        return 0;
    }
    private ushort IscIndirectXCycle1()
    {
        return 0;
    }

    private ushort IscIndirectYCycle8()
    {
        return 0;
    }
    private ushort IscIndirectYCycle7()
    {
        return 0;
    }
    private ushort IscIndirectYCycle6()
    {
        return 0;
    }
    private ushort IscIndirectYCycle5()
    {
        return 0;
    }
    private ushort IscIndirectYCycle4()
    {
        return 0;
    }
    private ushort IscIndirectYCycle3()
    {
        return 0;
    }
    private ushort IscIndirectYCycle2()
    {
        return 0;
    }
    private ushort IscIndirectYCycle1()
    {
        return 0;
    }

    // dcp
    private ushort DcpZeropageCycle5()
    {
        return 0;
    }
    private ushort DcpZeropageCycle4()
    {
        return 0;
    }
    private ushort DcpZeropageCycle3()
    {
        return 0;
    }
    private ushort DcpZeropageCycle2()
    {
        return 0;
    }
    private ushort DcpZeropageCycle1()
    {
        return 0;
    }


    private ushort DcpZeropageXCycle6()
    {
        return 0;
    }
    private ushort DcpZeropageXCycle5()
    {
        return 0;
    }
    private ushort DcpZeropageXCycle4()
    {
        return 0;
    }
    private ushort DcpZeropageXCycle3()
    {
        return 0;
    }
    private ushort DcpZeropageXCycle2()
    {
        return 0;
    }
    private ushort DcpZeropageXCycle1()
    {
        return 0;
    }

    private ushort DcpAbsoluteCycle6()
    {
        return 0;
    }
    private ushort DcpAbsoluteCycle5()
    {
        return 0;
    }
    private ushort DcpAbsoluteCycle4()
    {
        return 0;
    }
    private ushort DcpAbsoluteCycle3()
    {
        return 0;
    }
    private ushort DcpAbsoluteCycle2()
    {
        return 0;
    }
    private ushort DcpAbsoluteCycle1()
    {
        return 0;
    }

    private ushort DcpAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort DcpAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort DcpAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort DcpAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort DcpIndirectXCycle8()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle7()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle6()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle5()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle4()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle3()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle2()
    {
        return 0;
    }
    private ushort DcpIndirectXCycle1()
    {
        return 0;
    }

    private ushort DcpIndirectYCycle8()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle7()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle6()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle5()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle4()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle3()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle2()
    {
        return 0;
    }
    private ushort DcpIndirectYCycle1()
    {
        return 0;
    }

    // las

    private ushort LasCycle4()
    {
        return 0;
    }
    private ushort LasCycle3()
    {
        return 0;
    }
    private ushort LasCycle2()
    {
        return 0;
    }
    private ushort LasCycle1()
    {
        return 0;
    }

    // lxa
    private ushort LxaCycle2()
    {
        return 0;
    }
    private ushort LxaCycle1()
    {
        return 0;
    }

    // lax

    private ushort LaxZeropageCycle3()
    {
        return 0;
    }
    private ushort LaxZeropageCycle2()
    {
        return 0;
    }
    private ushort LaxZeropageCycle1()
    {
        return 0;
    }


    private ushort LaxZeropageYCycle4()
    {
        return 0;
    }
    private ushort LaxZeropageYCycle3()
    {
        return 0;
    }
    private ushort LaxZeropageYCycle2()
    {
        return 0;
    }
    private ushort LaxZeropageYCycle1()
    {
        return 0;
    }

    private ushort LaxAbsoluteCycle4()
    {
        return 0;
    }
    private ushort LaxAbsoluteCycle3()
    {
        return 0;
    }

    private ushort LaxAbsoluteCycle2()
    {
        return 0;
    }

    private ushort LaxAbsoluteCycle1()
    {
        return 0;
    }

    private ushort LaxAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort LaxAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort LaxAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort LaxAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort LaxAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort LaxIndirectXCycle6()
    {
        return 0;
    }
    private ushort LaxIndirectXCycle5()
    {
        return 0;
    }
    private ushort LaxIndirectXCycle4()
    {
        return 0;
    }
    private ushort LaxIndirectXCycle3()
    {
        return 0;
    }
    private ushort LaxIndirectXCycle2()
    {
        return 0;
    }
    private ushort LaxIndirectXCycle1()
    {
        return 0;
    }

    private ushort LaxIndirectYCycle6()
    {
        return 0;
    }
    private ushort LaxIndirectYCycle5()
    {
        return 0;
    }
    private ushort LaxIndirectYCycle4()
    {
        return 0;
    }
    private ushort LaxIndirectYCycle3()
    {
        return 0;
    }
    private ushort LaxIndirectYCycle2()
    {
        return 0;
    }
    private ushort LaxIndirectYCycle1()
    {
        return 0;
    }

    // shx
    private ushort ShxCycle5()
    {
        return 0;
    }
    private ushort ShxCycle4()
    {
        return 0;
    }
    private ushort ShxCycle3()
    {
        return 0;
    }
    private ushort ShxCycle2()
    {
        return 0;
    }
    private ushort ShxCycle1()
    {
        return 0;
    }

    // shy
    private ushort ShyCycle5()
    {
        return 0;
    }
    private ushort ShyCycle4()
    {
        return 0;
    }
    private ushort ShyCycle3()
    {
        return 0;
    }
    private ushort ShyCycle2()
    {
        return 0;
    }
    private ushort ShyCycle1()
    {
        return 0;
    }

    // tas
    private ushort TasCycle5()
    {
        return 0;
    }
    private ushort TasCycle4()
    {
        return 0;
    }
    private ushort TasCycle3()
    {
        return 0;
    }
    private ushort TasCycle2()
    {
        return 0;
    }
    private ushort TasCycle1()
    {
        return 0;
    }

    // SHA
    private ushort ShaIndirectYCycle5()
    {
        return 0;
    }
    private ushort ShaIndirectYCycle4()
    {
        return 0;
    }
    private ushort ShaIndirectYCycle3()
    {
        return 0;
    }
    private ushort ShaIndirectYCycle2()
    {
        return 0;
    }
    private ushort ShaIndirectYCycle1()
    {
        return 0;
    }

    private ushort ShaAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort ShaAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort ShaAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort ShaAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort ShaAbsoluteYCycle1()
    {
        return 0;
    }

    // JAM (illegal opcode).
    private ushort Jam()
    {
        return 0;
    }

    private ushort TyaCycle2()
    {
        return 0;
    }
    private ushort TyaCycle1()
    {
        return 0;
    }

    private ushort TxsCycle2()
    {
        return 0;
    }
    private ushort TxsCycle1()
    {
        return 0;
    }

    private ushort AneCycle2()
    {
        return 0;
    }
    private ushort AneCycle1()
    {
        return 0;
    }

    // NOPs (illegal opcodes).
    private ushort NopImpliedCycle2()
    {
        return 0;
    }
    private ushort NopImpliedCycle1()
    {
        return 0;
    }

    private ushort NopImmCycle2()
    {
        return 0;
    }
    private ushort NopImmCycle1()
    {
        return 0;
    }

    private ushort NopZeropageCycle3()
    {
        return 0;
    }
    private ushort NopZeropageCycle2()
    {
        return 0;
    }
    private ushort NopZeropageCycle1()
    {
        return 0;
    }

    private ushort NopZeropageXCycle4()
    {
        return 0;
    }
    private ushort NopZeropageXCycle3()
    {
        return 0;
    }
    private ushort NopZeropageXCycle2()
    {
        return 0;
    }
    private ushort NopZeropageXCycle1()
    {
        return 0;
    }

    private ushort NopAbsoluteCycle4()
    {
        return 0;
    }
    private ushort NopAbsoluteCycle3()
    {
        return 0;
    }
    private ushort NopAbsoluteCycle2()
    {
        return 0;
    }
    private ushort NopAbsoluteCycle1()
    {
        return 0;
    }

    private ushort NopAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort NopAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort NopAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort NopAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort NopAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort AncCycle3()
    {
        return 0;
    }
    private ushort AncCycle2()
    {
        return 0;
    }
    private ushort AncCycle1()
    {
        return 0;
    }

    private ushort ArrCycle2()
    {
        return 0;
    }
    private ushort ArrCycle1()
    {
        return 0;
    }

    // Proper CPU instruction steps.
    private ushort ClcCycle2()
    {
        return 0;
    }
    private ushort ClcCycle1()
    {
        return 0;
    }

    private ushort SeiCycle2()
    {
        return 0;
    }
    private ushort SeiCycle1()
    {
        return 0;
    }

    private ushort PlaCycle4()
    {
        return 0;
    }
    private ushort PlaCycle3()
    {
        return 0;
    }
    private ushort PlaCycle2()
    {
        return 0;
    }
    private ushort PlaCycle1()
    {
        return 0;
    }

    private ushort DeyCycle2()
    {
        return 0;
    }
    private ushort DeyCycle1()
    {
        return 0;
    }

    private ushort TxaCycle2()
    {
        return 0;
    }
    private ushort TxaCycle1()
    {
        return 0;
    }

    private ushort SecCycle2()
    {
        return 0;
    }
    private ushort SecCycle1()
    {
        return 0;
    }

    private ushort PlpCycle4()
    {
        return 0;
    }
    private ushort PlpCycle3()
    {
        return 0;
    }
    private ushort PlpCycle2()
    {
        return 0;
    }
    private ushort PlpCycle1()
    {
        return 0;
    }

    private ushort PhaCycle3()
    {
        return 0;
    }
    private ushort PhaCycle2()
    {
        return 0;
    }
    private ushort PhaCycle1()
    {
        return 0;
    }

    private ushort DexCycle2()
    {
        return 0;
    }
    private ushort DexCycle1()
    {
        return 0;
    }

    private ushort InyCycle2()
    {
        return 0;
    }
    private ushort InyCycle1()
    {
        return 0;
    }

    private ushort CldCycle2()
    {
        return 0;
    }
    private ushort CldCycle1()
    {
        return 0;
    }

    private ushort InxCycle2()
    {
        return 0;
    }
    private ushort InxCycle1()
    {
        return 0;
    }

    private ushort SedCycle2()
    {
        return 0;
    }
    private ushort SedCycle1()
    {
        return 0;
    }

    private ushort TaxCycle2()
    {
        return 0;
    }
    private ushort TaxCycle1()
    {
        return 0;
    }

    private ushort TsxCycle2()
    {
        return 0;
    }
    private ushort TsxCycle1()
    {
        return 0;
    }

    private ushort TayCycle2()
    {
        return 0;
    }
    private ushort TayCycle1()
    {
        return 0;
    }

    private ushort ClvCycle2()
    {
        return 0;
    }
    private ushort ClvCycle1()
    {
        return 0;
    }

    private ushort CliCycle2()
    {
        return 0;
    }
    private ushort CliCycle1()
    {
        return 0;
    }

    private ushort PhpCycle3()
    {
        return 0;
    }
    private ushort PhpCycle2()
    {
        return 0;
    }
    private ushort PhpCycle1()
    {
        return 0;
    }

    private ushort BrkCycle7()
    {
        return 0;
    }
    private ushort BrkCycle6()
    {
        return 0;
    }
    private ushort BrkCycle5()
    {
        return 0;
    }
    private ushort BrkCycle4()
    {
        return 0;
    }
    private ushort BrkCycle3()
    {
        return 0;
    }
    private ushort BrkCycle2()
    {
        return 0;
    }
    private ushort BrkCycle1()
    {
        return 0;
    }

    private ushort BplCycle4()
    {
        return 0;
    }
    private ushort BplCycle3()
    {
        return 0;
    }
    private ushort BplCycle2()
    {
        return 0;
    }
    private ushort BplCycle1()
    {
        return 0;
    }

    private ushort JsrCycle6()
    {
        return 0;
    }
    private ushort JsrCycle5()
    {
        return 0;
    }
    private ushort JsrCycle4()
    {
        return 0;
    }
    private ushort JsrCycle3()
    {
        return 0;
    }
    private ushort JsrCycle2()
    {
        return 0;
    }
    private ushort JsrCycle1()
    {
        return 0;
    }

    private ushort BmiCycle4()
    {
        return 0;
    }
    private ushort BmiCycle3()
    {
        return 0;
    }
    private ushort BmiCycle2()
    {
        return 0;
    }
    private ushort BmiCycle1()
    {
        return 0;
    }

    private ushort RtiCycle6()
    {
        return 0;
    }
    private ushort RtiCycle5()
    {
        return 0;
    }
    private ushort RtiCycle4()
    {
        return 0;
    }
    private ushort RtiCycle3()
    {
        return 0;
    }
    private ushort RtiCycle2()
    {
        return 0;
    }
    private ushort RtiCycle1()
    {
        return 0;
    }

    private ushort BvcCycle4()
    {
        return 0;
    }
    private ushort BvcCycle3()
    {
        return 0;
    }
    private ushort BvcCycle2()
    {
        return 0;
    }
    private ushort BvcCycle1()
    {
        return 0;
    }

    private ushort BvsCycle4()
    {
        return 0;
    }
    private ushort BvsCycle3()
    {
        return 0;
    }
    private ushort BvsCycle2()
    {
        return 0;
    }
    private ushort BvsCycle1()
    {
        return 0;
    }


    private ushort RtsCycle6()
    {
        return 0;
    }
    private ushort RtsCycle5()
    {
        return 0;
    }
    private ushort RtsCycle4()
    {
        return 0;
    }
    private ushort RtsCycle3()
    {
        return 0;
    }
    private ushort RtsCycle2()
    {
        return 0;
    }
    private ushort RtsCycle1()
    {
        return 0;
    }

    private ushort BccCycle4()
    {
        return 0;
    }
    private ushort BccCycle3()
    {
        return 0;
    }
    private ushort BccCycle2()
    {
        return 0;
    }
    private ushort BccCycle1()
    {
        return 0;
    }

    private ushort LdyImmCycle2()
    {
        return 0;
    }
    private ushort LdyImmCycle1()
    {
        return 0;
    }

    private ushort LdyZeropageCycle3()
    {
        return 0;
    }
    private ushort LdyZeropageCycle2()
    {
        return 0;
    }
    private ushort LdyZeropageCycle1()
    {
        return 0;
    }

    private ushort LdyZeropageXCycle4()
    {
        return 0;
    }
    private ushort LdyZeropageXCycle3()
    {
        return 0;
    }
    private ushort LdyZeropageXCycle2()
    {
        return 0;
    }
    private ushort LdyZeropageXCycle1()
    {
        return 0;
    }

    private ushort LdyAbsoluteCycle4()
    {
        return 0;
    }
    private ushort LdyAbsoluteCycle3()
    {
        return 0;
    }
    private ushort LdyAbsoluteCycle2()
    {
        return 0;
    }
    private ushort LdyAbsoluteCycle1()
    {
        return 0;
    }

    private ushort LdyAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort LdyAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort LdyAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort LdyAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort LdyAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort BcsCycle4()
    {
        return 0;
    }
    private ushort BcsCycle3()
    {
        return 0;
    }
    private ushort BcsCycle2()
    {
        return 0;
    }
    private ushort BcsCycle1()
    {
        return 0;
    }

    private ushort CpyImmCycle2()
    {
        return 0;
    }
    private ushort CpyImmCycle1()
    {
        return 0;
    }

    private ushort CpyZeropageCycle3()
    {
        return 0;
    }
    private ushort CpyZeropageCycle2()
    {
        return 0;
    }
    private ushort CpyZeropageCycle1()
    {
        return 0;
    }

    private ushort CpyAbsoluteCycle4()
    {
        return 0;
    }
    private ushort CpyAbsoluteCycle3()
    {
        return 0;
    }
    private ushort CpyAbsoluteCycle2()
    {
        return 0;
    }
    private ushort CpyAbsoluteCycle1()
    {
        return 0;
    }

    private ushort BneCycle4()
    {
        return 0;
    }
    private ushort BneCycle3()
    {
        return 0;
    }
    private ushort BneCycle2()
    {
        return 0;
    }
    private ushort BneCycle1()
    {
        return 0;
    }

    private ushort CpxImmCycle2()
    {
        return 0;
    }
    private ushort CpxImmCycle1()
    {
        return 0;
    }

    private ushort CpxZeropageCycle3()
    {
        return 0;
    }
    private ushort CpxZeropageCycle2()
    {
        return 0;
    }
    private ushort CpxZeropageCycle1()
    {
        return 0;
    }

    private ushort CpxAbsoluteCycle4()
    {
        return 0;
    }
    private ushort CpxAbsoluteCycle3()
    {
        return 0;
    }
    private ushort CpxAbsoluteCycle2()
    {
        return 0;
    }
    private ushort CpxAbsoluteCycle1()
    {
        return 0;
    }

    private ushort BeqCycle4()
    {
        return 0;
    }
    private ushort BeqCycle3()
    {
        return 0;
    }
    private ushort BeqCycle2()
    {
        return 0;
    }
    private ushort BeqCycle1()
    {
        return 0;
    }

    private ushort OraImmCycle2()
    {
        return 0;
    }

    private ushort OraImmCycle1()
    {
        return 0;
    }

    private ushort OraZeropageCycle3()
    {
        return 0;
    }
    private ushort OraZeropageCycle2()
    {
        return 0;
    }
    private ushort OraZeropageCycle1()
    {
        return 0;
    }

    private ushort OraZeropageXCycle4()
    {
        return 0;
    }
    private ushort OraZeropageXCycle3()
    {
        return 0;
    }
    private ushort OraZeropageXCycle2()
    {
        return 0;
    }
    private ushort OraZeropageXCycle1()
    {
        return 0;
    }

    private ushort OraAbsoluteCycle4()
    {
        return 0;
    }
    private ushort OraAbsoluteCycle3()
    {
        return 0;
    }
    private ushort OraAbsoluteCycle2()
    {
        return 0;
    }
    private ushort OraAbsoluteCycle1()
    {
        return 0;
    }

    private ushort OraAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort OraAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort OraAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort OraAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort OraAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort OraAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort OraAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort OraAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort OraAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort OraAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort OraIndirectXCycle6()
    {
        return 0;
    }
    private ushort OraIndirectXCycle5()
    {
        return 0;
    }
    private ushort OraIndirectXCycle4()
    {
        return 0;
    }
    private ushort OraIndirectXCycle3()
    {
        return 0;
    }
    private ushort OraIndirectXCycle2()
    {
        return 0;
    }
    private ushort OraIndirectXCycle1()
    {
        return 0;
    }

    private ushort OraIndirectYCycle6()
    {
        return 0;
    }
    private ushort OraIndirectYCycle5()
    {
        return 0;
    }
    private ushort OraIndirectYCycle4()
    {
        return 0;
    }
    private ushort OraIndirectYCycle3()
    {
        return 0;
    }
    private ushort OraIndirectYCycle2()
    {
        return 0;
    }
    private ushort OraIndirectYCycle1()
    {
        return 0;
    }


    private ushort AndImmCycle2()
    {
        return 0;
    }
    private ushort AndImmCycle1()
    {
        return 0;
    }

    private ushort AndZeropageCycle3()
    {
        return 0;
    }
    private ushort AndZeropageCycle2()
    {
        return 0;
    }
    private ushort AndZeropageCycle1()
    {
        return 0;
    }

    private ushort AndZeropageXCycle4()
    {
        return 0;
    }
    private ushort AndZeropageXCycle3()
    {
        return 0;
    }
    private ushort AndZeropageXCycle2()
    {
        return 0;
    }
    private ushort AndZeropageXCycle1()
    {
        return 0;
    }

    private ushort AndAbsoluteCycle4()
    {
        return 0;
    }
    private ushort AndAbsoluteCycle3()
    {
        return 0;
    }
    private ushort AndAbsoluteCycle2()
    {
        return 0;
    }
    private ushort AndAbsoluteCycle1()
    {
        return 0;
    }

    private ushort AndAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort AndAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort AndAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort AndAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort AndAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort AndAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort AndAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort AndAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort AndAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort AndAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort AndIndirectXCycle6()
    {
        return 0;
    }
    private ushort AndIndirectXCycle5()
    {
        return 0;
    }
    private ushort AndIndirectXCycle4()
    {
        return 0;
    }
    private ushort AndIndirectXCycle3()
    {
        return 0;
    }
    private ushort AndIndirectXCycle2()
    {
        return 0;
    }
    private ushort AndIndirectXCycle1()
    {
        return 0;
    }

    private ushort AndIndirectYCycle6()
    {
        return 0;
    }
    private ushort AndIndirectYCycle5()
    {
        return 0;
    }
    private ushort AndIndirectYCycle4()
    {
        return 0;
    }
    private ushort AndIndirectYCycle3()
    {
        return 0;
    }
    private ushort AndIndirectYCycle2()
    {
        return 0;
    }
    private ushort AndIndirectYCycle1()
    {
        return 0;
    }

    private ushort EorImmCycle2()
    {
        return 0;
    }
    private ushort EorImmCycle1()
    {
        return 0;
    }

    private ushort EorZeropageCycle3()
    {
        return 0;
    }
    private ushort EorZeropageCycle2()
    {
        return 0;
    }
    private ushort EorZeropageCycle1()
    {
        return 0;
    }

    private ushort EorZeropageXCycle4()
    {
        return 0;
    }
    private ushort EorZeropageXCycle3()
    {
        return 0;
    }
    private ushort EorZeropageXCycle2()
    {
        return 0;
    }
    private ushort EorZeropageXCycle1()
    {
        return 0;
    }

    private ushort EorAbsoluteCycle4()
    {
        return 0;
    }
    private ushort EorAbsoluteCycle3()
    {
        return 0;
    }
    private ushort EorAbsoluteCycle2()
    {
        return 0;
    }
    private ushort EorAbsoluteCycle1()
    {
        return 0;
    }

    private ushort EorAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort EorAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort EorAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort EorAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort EorAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort EorAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort EorAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort EorAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort EorAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort EorAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort EorIndirectXCycle6()
    {
        return 0;
    }
    private ushort EorIndirectXCycle5()
    {
        return 0;
    }
    private ushort EorIndirectXCycle4()
    {
        return 0;
    }
    private ushort EorIndirectXCycle3()
    {
        return 0;
    }
    private ushort EorIndirectXCycle2()
    {
        return 0;
    }
    private ushort EorIndirectXCycle1()
    {
        return 0;
    }

    private ushort EorIndirectYCycle6()
    {
        return 0;
    }
    private ushort EorIndirectYCycle5()
    {
        return 0;
    }
    private ushort EorIndirectYCycle4()
    {
        return 0;
    }
    private ushort EorIndirectYCycle3()
    {
        return 0;
    }
    private ushort EorIndirectYCycle2()
    {
        return 0;
    }
    private ushort EorIndirectYCycle1()
    {
        return 0;
    }

    private ushort StaZeropageCycle3()
    {
        return 0;
    }
    private ushort StaZeropageCycle2()
    {
        return 0;
    }
    private ushort StaZeropageCycle1()
    {
        return 0;
    }

    private ushort StaZeropageXCycle4()
    {
        return 0;
    }
    private ushort StaZeropageXCycle3()
    {
        return 0;
    }
    private ushort StaZeropageXCycle2()
    {
        return 0;
    }
    private ushort StaZeropageXCycle1()
    {
        return 0;
    }

    private ushort StaAbsoluteCycle4()
    {
        return 0;
    }
    private ushort StaAbsoluteCycle3()
    {
        return 0;
    }
    private ushort StaAbsoluteCycle2()
    {
        return 0;
    }
    private ushort StaAbsoluteCycle1()
    {
        return 0;
    }

    private ushort StaAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort StaAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort StaAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort StaAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort StaAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort StaAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort StaAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort StaAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort StaAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort StaAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort StaIndirectXCycle6()
    {
        return 0;
    }
    private ushort StaIndirectXCycle5()
    {
        return 0;
    }
    private ushort StaIndirectXCycle4()
    {
        return 0;
    }
    private ushort StaIndirectXCycle3()
    {
        return 0;
    }
    private ushort StaIndirectXCycle2()
    {
        return 0;
    }
    private ushort StaIndirectXCycle1()
    {
        return 0;
    }

    private ushort StaIndirectYCycle6()
    {
        return 0;
    }
    private ushort StaIndirectYCycle5()
    {
        return 0;
    }
    private ushort StaIndirectYCycle4()
    {
        return 0;
    }
    private ushort StaIndirectYCycle3()
    {
        return 0;
    }
    private ushort StaIndirectYCycle2()
    {
        return 0;
    }
    private ushort StaIndirectYCycle1()
    {
        return 0;
    }

    private ushort LdaImmCycle2()
    {
        return 0;
    }
    private ushort LdaImmCycle1()
    {
        return 0;
    }

    private ushort LdaZeropageCycle3()
    {
        return 0;
    }
    private ushort LdaZeropageCycle2()
    {
        return 0;
    }
    private ushort LdaZeropageCycle1()
    {
        return 0;
    }

    private ushort LdaZeropageXCycle4()
    {
        return 0;
    }
    private ushort LdaZeropageXCycle3()
    {
        return 0;
    }
    private ushort LdaZeropageXCycle2()
    {
        return 0;
    }
    private ushort LdaZeropageXCycle1()
    {
        return 0;
    }

    private ushort LdaAbsoluteCycle4()
    {
        return 0;
    }
    private ushort LdaAbsoluteCycle3()
    {
        return 0;
    }
    private ushort LdaAbsoluteCycle2()
    {
        return 0;
    }
    private ushort LdaAbsoluteCycle1()
    {
        return 0;
    }

    private ushort LdaAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort LdaAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort LdaAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort LdaAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort LdaAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort LdaAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort LdaAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort LdaAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort LdaAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort LdaAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort LdaIndirectXCycle6()
    {
        return 0;
    }
    private ushort LdaIndirectXCycle5()
    {
        return 0;
    }
    private ushort LdaIndirectXCycle4()
    {
        return 0;
    }
    private ushort LdaIndirectXCycle3()
    {
        return 0;
    }
    private ushort LdaIndirectXCycle2()
    {
        return 0;
    }
    private ushort LdaIndirectXCycle1()
    {
        return 0;
    }

    private ushort LdaIndirectYCycle6()
    {
        return 0;
    }
    private ushort LdaIndirectYCycle5()
    {
        return 0;
    }
    private ushort LdaIndirectYCycle4()
    {
        return 0;
    }
    private ushort LdaIndirectYCycle3()
    {
        return 0;
    }
    private ushort LdaIndirectYCycle2()
    {
        return 0;
    }
    private ushort LdaIndirectYCycle1()
    {
        return 0;
    }



    private ushort CmpImmCycle2()
    {
        return 0;
    }
    private ushort CmpImmCycle1()
    {
        return 0;
    }

    private ushort CmpZeropageCycle3()
    {
        return 0;
    }
    private ushort CmpZeropageCycle2()
    {
        return 0;
    }
    private ushort CmpZeropageCycle1()
    {
        return 0;
    }

    private ushort CmpZeropageXCycle4()
    {
        return 0;
    }
    private ushort CmpZeropageXCycle3()
    {
        return 0;
    }
    private ushort CmpZeropageXCycle2()
    {
        return 0;
    }
    private ushort CmpZeropageXCycle1()
    {
        return 0;
    }

    private ushort CmpAbsoluteCycle4()
    {
        return 0;
    }
    private ushort CmpAbsoluteCycle3()
    {
        return 0;
    }
    private ushort CmpAbsoluteCycle2()
    {
        return 0;
    }
    private ushort CmpAbsoluteCycle1()
    {
        return 0;
    }

    private ushort CmpAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort CmpAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort CmpAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort CmpAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort CmpAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort CmpAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort CmpAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort CmpAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort CmpAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort CmpAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort CmpIndirectXCycle6()
    {
        return 0;
    }
    private ushort CmpIndirectXCycle5()
    {
        return 0;
    }
    private ushort CmpIndirectXCycle4()
    {
        return 0;
    }
    private ushort CmpIndirectXCycle3()
    {
        return 0;
    }
    private ushort CmpIndirectXCycle2()
    {
        return 0;
    }
    private ushort CmpIndirectXCycle1()
    {
        return 0;
    }

    private ushort CmpIndirectYCycle6()
    {
        return 0;
    }
    private ushort CmpIndirectYCycle5()
    {
        return 0;
    }
    private ushort CmpIndirectYCycle4()
    {
        return 0;
    }
    private ushort CmpIndirectYCycle3()
    {
        return 0;
    }
    private ushort CmpIndirectYCycle2()
    {
        return 0;
    }
    private ushort CmpIndirectYCycle1()
    {
        return 0;
    }

    private ushort SbcImmCycle2()
    {
        return 0;
    }
    private ushort SbcImmCycle1()
    {
        return 0;
    }

    private ushort SbcZeropageCycle3()
    {
        return 0;
    }
    private ushort SbcZeropageCycle2()
    {
        return 0;
    }
    private ushort SbcZeropageCycle1()
    {
        return 0;
    }

    private ushort SbcZeropageXCycle4()
    {
        return 0;
    }
    private ushort SbcZeropageXCycle3()
    {
        return 0;
    }
    private ushort SbcZeropageXCycle2()
    {
        return 0;
    }
    private ushort SbcZeropageXCycle1()
    {
        return 0;
    }

    private ushort SbcAbsoluteCycle4()
    {
        return 0;
    }
    private ushort SbcAbsoluteCycle3()
    {
        return 0;
    }
    private ushort SbcAbsoluteCycle2()
    {
        return 0;
    }
    private ushort SbcAbsoluteCycle1()
    {
        return 0;
    }

    private ushort SbcAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort SbcAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort SbcAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort SbcAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort SbcAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort SbcAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort SbcAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort SbcAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort SbcAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort SbcAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort SbcIndirectXCycle6()
    {
        return 0;
    }
    private ushort SbcIndirectXCycle5()
    {
        return 0;
    }
    private ushort SbcIndirectXCycle4()
    {
        return 0;
    }
    private ushort SbcIndirectXCycle3()
    {
        return 0;
    }
    private ushort SbcIndirectXCycle2()
    {
        return 0;
    }
    private ushort SbcIndirectXCycle1()
    {
        return 0;
    }

    private ushort SbcIndirectYCycle6()
    {
        return 0;
    }
    private ushort SbcIndirectYCycle5()
    {
        return 0;
    }
    private ushort SbcIndirectYCycle4()
    {
        return 0;
    }
    private ushort SbcIndirectYCycle3()
    {
        return 0;
    }
    private ushort SbcIndirectYCycle2()
    {
        return 0;
    }
    private ushort SbcIndirectYCycle1()
    {
        return 0;
    }

    private ushort LdxImmCycle2()
    {
        return 0;
    }
    private ushort LdxImmCycle1()
    {
        return 0;
    }

    private ushort LdxZeropageYCycle1()
    {
        return 0;
    }
    
    private ushort LdxZeropageYCycle2()
    {
        return 0;
    }
    
    private ushort LdxZeropageYCycle3()
    {
        return 0;
    }
    
    private ushort LdxZeropageYCycle4()
    {
        return 0;
    }

    private ushort LdxZeropageCycle3()
    {
        return 0;
    }
    private ushort LdxZeropageCycle2()
    {
        return 0;
    }
    private ushort LdxZeropageCycle1()
    {
        return 0;
    }


    private ushort LdxAbsoluteCycle4()
    {
        return 0;
    }
    private ushort LdxAbsoluteCycle3()
    {
        return 0;
    }
    private ushort LdxAbsoluteCycle2()
    {
        return 0;
    }
    private ushort LdxAbsoluteCycle1()
    {
        return 0;
    }

    private ushort LdxAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort LdxAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort LdxAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort LdxAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort LdxAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort BitZeropageCycle3()
    {
        return 0;
    }
    private ushort BitZeropageCycle2()
    {
        return 0;
    }
    private ushort BitZeropageCycle1()
    {
        return 0;
    }

    private ushort BitAbsoluteCycle4()
    {
        return 0;
    }
    private ushort BitAbsoluteCycle3()
    {
        return 0;
    }
    private ushort BitAbsoluteCycle2()
    {
        return 0;
    }
    private ushort BitAbsoluteCycle1()
    {
        return 0;
    }

    private ushort StyZeropageCycle3()
    {
        return 0;
    }
    private ushort StyZeropageCycle2()
    {
        return 0;
    }
    private ushort StyZeropageCycle1()
    {
        return 0;
    }

    private ushort StyZeropageXCycle4()
    {
        return 0;
    }
    private ushort StyZeropageXCycle3()
    {
        return 0;
    }
    private ushort StyZeropageXCycle2()
    {
        return 0;
    }
    private ushort StyZeropageXCycle1()
    {
        return 0;
    }

    private ushort StyAbsoluteCycle4()
    {
        return 0;
    }
    private ushort StyAbsoluteCycle3()
    {
        return 0;
    }
    private ushort StyAbsoluteCycle2()
    {
        return 0;
    }
    private ushort StyAbsoluteCycle1()
    {
        return 0;
    }

    private ushort AdcImmCycle2()
    {
        return 0;
    }
    private ushort AdcImmCycle1()
    {
        return 1;
    }

    private ushort AdcZeropageCycle3()
    {
        return 0;
    }
    private ushort AdcZeropageCycle2()
    {
        return 0;
    }
    private ushort AdcZeropageCycle1()
    {
        return 0;
    }

    private ushort AdcZeropageXCycle4()
    {
        return 0;
    }
    private ushort AdcZeropageXCycle3()
    {
        return 0;
    }
    private ushort AdcZeropageXCycle2()
    {
        return 0;
    }
    private ushort AdcZeropageXCycle1()
    {
        return 0;
    }

    private ushort AdcAbsoluteCycle4()
    {
        return 0;
    }
    private ushort AdcAbsoluteCycle3()
    {
        return 0;
    }
    private ushort AdcAbsoluteCycle2()
    {
        return 0;
    }
    private ushort AdcAbsoluteCycle1()
    {
        return 0;
    }

    private ushort AdcAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort AdcAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort AdcAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort AdcAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort AdcAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort AdcAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort AdcAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort AdcAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort AdcAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort AdcAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort AdcIndirectXCycle6()
    {
        return 0;
    }
    private ushort AdcIndirectXCycle5()
    {
        return 0;
    }
    private ushort AdcIndirectXCycle4()
    {
        return 0;
    }
    private ushort AdcIndirectXCycle3()
    {
        return 0;
    }
    private ushort AdcIndirectXCycle2()
    {
        return 0;
    }
    private ushort AdcIndirectXCycle1()
    {
        return 0;
    }

    private ushort AdcIndirectYCycle6()
    {
        return 0;
    }
    private ushort AdcIndirectYCycle5()
    {
        return 0;
    }
    private ushort AdcIndirectYCycle4()
    {
        return 0;
    }
    private ushort AdcIndirectYCycle3()
    {
        return 0;
    }
    private ushort AdcIndirectYCycle2()
    {
        return 0;
    }
    private ushort AdcIndirectYCycle1()
    {
        return 0;
    }

    private ushort AslAccumCycle2()
    {
        return 0;
    }
    private ushort AslAccumCycle1()
    {
        return 0;
    }

    private ushort AslZeropageCycle5()
    {
        return 0;
    }
    private ushort AslZeropageCycle4()
    {
        return 0;
    }
    private ushort AslZeropageCycle3()
    {
        return 0;
    }
    private ushort AslZeropageCycle2()
    {
        return 0;
    }
    private ushort AslZeropageCycle1()
    {
        return 0;
    }

    private ushort AslZeropageXCycle6()
    {
        return 0;
    }
    private ushort AslZeropageXCycle5()
    {
        return 0;
    }
    private ushort AslZeropageXCycle4()
    {
        return 0;
    }
    private ushort AslZeropageXCycle3()
    {
        return 0;
    }
    private ushort AslZeropageXCycle2()
    {
        return 0;
    }
    private ushort AslZeropageXCycle1()
    {
        return 0;
    }

    private ushort AslAbsoluteCycle6()
    {
        return 0;
    }
    private ushort AslAbsoluteCycle5()
    {
        return 0;
    }
    private ushort AslAbsoluteCycle4()
    {
        return 0;
    }
    private ushort AslAbsoluteCycle3()
    {
        return 0;
    }
    private ushort AslAbsoluteCycle2()
    {
        return 0;
    }
    private ushort AslAbsoluteCycle1()
    {
        return 0;
    }

    private ushort AslAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort AslAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort RolAccumCycle2()
    {
        return 0;
    }
    private ushort RolAccumCycle1()
    {
        return 0;
    }

    private ushort RolZeropageCycle5()
    {
        return 0;
    }
    private ushort RolZeropageCycle4()
    {
        return 0;
    }
    private ushort RolZeropageCycle3()
    {
        return 0;
    }
    private ushort RolZeropageCycle2()
    {
        return 0;
    }
    private ushort RolZeropageCycle1()
    {
        return 0;
    }

    private ushort RolZeropageXCycle6()
    {
        return 0;
    }
    private ushort RolZeropageXCycle5()
    {
        return 0;
    }
    private ushort RolZeropageXCycle4()
    {
        return 0;
    }
    private ushort RolZeropageXCycle3()
    {
        return 0;
    }
    private ushort RolZeropageXCycle2()
    {
        return 0;
    }
    private ushort RolZeropageXCycle1()
    {
        return 0;
    }

    private ushort RolAbsoluteCycle6()
    {
        return 0;
    }
    private ushort RolAbsoluteCycle5()
    {
        return 0;
    }
    private ushort RolAbsoluteCycle4()
    {
        return 0;
    }
    private ushort RolAbsoluteCycle3()
    {
        return 0;
    }
    private ushort RolAbsoluteCycle2()
    {
        return 0;
    }
    private ushort RolAbsoluteCycle1()
    {
        return 0;
    }

    private ushort RolAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort RolAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort RorAccumCycle2()
    {
        return 0;
    }
    private ushort RorAccumCycle1()
    {
        return 0;
    }

    private ushort RorZeropageCycle5()
    {
        return 0;
    }
    private ushort RorZeropageCycle4()
    {
        return 0;
    }
    private ushort RorZeropageCycle3()
    {
        return 0;
    }
    private ushort RorZeropageCycle2()
    {
        return 0;
    }
    private ushort RorZeropageCycle1()
    {
        return 0;
    }

    private ushort RorZeropageXCycle6()
    {
        return 0;
    }
    private ushort RorZeropageXCycle5()
    {
        return 0;
    }
    private ushort RorZeropageXCycle4()
    {
        return 0;
    }
    private ushort RorZeropageXCycle3()
    {
        return 0;
    }
    private ushort RorZeropageXCycle2()
    {
        return 0;
    }
    private ushort RorZeropageXCycle1()
    {
        return 0;
    }

    private ushort RorAbsoluteCycle6()
    {
        return 0;
    }
    private ushort RorAbsoluteCycle5()
    {
        return 0;
    }
    private ushort RorAbsoluteCycle4()
    {
        return 0;
    }
    private ushort RorAbsoluteCycle3()
    {
        return 0;
    }
    private ushort RorAbsoluteCycle2()
    {
        return 0;
    }
    private ushort RorAbsoluteCycle1()
    {
        return 0;
    }

    private ushort RorAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort RorAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort SloAbsoluteCycle6()
    {
        return 0;
    }
    private ushort SloAbsoluteCycle5()
    {
        return 0;
    }
    private ushort SloAbsoluteCycle4()
    {
        return 0;
    }
    private ushort SloAbsoluteCycle3()
    {
        return 0;
    }
    private ushort SloAbsoluteCycle2()
    {
        return 0;
    }
    private ushort SloAbsoluteCycle1()
    {
        return 0;
    }

    private ushort SloAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort SloAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort SloAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort SloAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort SloZeropageCycle5()
    {
        return 0;
    }
    private ushort SloZeropageCycle4()
    {
        return 0;
    }
    private ushort SloZeropageCycle3()
    {
        return 0;
    }
    private ushort SloZeropageCycle2()
    {
        return 0;
    }
    private ushort SloZeropageCycle1()
    {
        return 0;
    }

    private ushort SloIndirectXCycle8()
    {
        return 0;
    }
    private ushort SloIndirectXCycle7()
    {
        return 0;
    }

    private ushort SloIndirectXCycle6()
    {
        return 0;
    }
    private ushort SloIndirectXCycle5()
    {
        return 0;
    }
    private ushort SloIndirectXCycle4()
    {
        return 0;
    }
    private ushort SloIndirectXCycle3()
    {
        return 0;
    }
    private ushort SloIndirectXCycle2()
    {
        return 0;
    }
    private ushort SloIndirectXCycle1()
    {
        return 0;
    }

    private ushort SloIndirectYCycle8()
    {
        return 0;
    }
    private ushort SloIndirectYCycle7()
    {
        return 0;
    }

    private ushort SloIndirectYCycle6()
    {
        return 0;
    }
    private ushort SloIndirectYCycle5()
    {
        return 0;
    }
    private ushort SloIndirectYCycle4()
    {
        return 0;
    }
    private ushort SloIndirectYCycle3()
    {
        return 0;
    }
    private ushort SloIndirectYCycle2()
    {
        return 0;
    }
    private ushort SloIndirectYCycle1()
    {
        return 0;
    }

    private ushort RlaAbsoluteCycle6()
    {
        return 0;
    }
    private ushort RlaAbsoluteCycle5()
    {
        return 0;
    }
    private ushort RlaAbsoluteCycle4()
    {
        return 0;
    }
    private ushort RlaAbsoluteCycle3()
    {
        return 0;
    }
    private ushort RlaAbsoluteCycle2()
    {
        return 0;
    }
    private ushort RlaAbsoluteCycle1()
    {
        return 0;
    }

    private ushort RlaAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort RlaAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort RlaAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort RlaAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort RlaZeropageCycle5()
    {
        return 0;
    }
    private ushort RlaZeropageCycle4()
    {
        return 0;
    }
    private ushort RlaZeropageCycle3()
    {
        return 0;
    }
    private ushort RlaZeropageCycle2()
    {
        return 0;
    }
    private ushort RlaZeropageCycle1()
    {
        return 0;
    }

    private ushort RlaZeropageXCycle6()
    {
        return 0;
    }
    private ushort RlaZeropageXCycle5()
    {
        return 0;
    }
    private ushort RlaZeropageXCycle4()
    {
        return 0;
    }
    private ushort RlaZeropageXCycle3()
    {
        return 0;
    }
    private ushort RlaZeropageXCycle2()
    {
        return 0;
    }
    private ushort RlaZeropageXCycle1()
    {
        return 0;
    }

    private ushort RlaIndirectXCycle8()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle7()
    {
        return 0;
    }

    private ushort RlaIndirectXCycle6()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle5()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle4()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle3()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle2()
    {
        return 0;
    }
    private ushort RlaIndirectXCycle1()
    {
        return 0;
    }

    private ushort RlaIndirectYCycle8()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle7()
    {
        return 0;
    }

    private ushort RlaIndirectYCycle6()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle5()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle4()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle3()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle2()
    {
        return 0;
    }
    private ushort RlaIndirectYCycle1()
    {
        return 0;
    }

    private ushort SreAbsoluteCycle6()
    {
        return 0;
    }
    private ushort SreAbsoluteCycle5()
    {
        return 0;
    }
    private ushort SreAbsoluteCycle4()
    {
        return 0;
    }
    private ushort SreAbsoluteCycle3()
    {
        return 0;
    }
    private ushort SreAbsoluteCycle2()
    {
        return 0;
    }
    private ushort SreAbsoluteCycle1()
    {
        return 0;
    }

    private ushort SreAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort SreAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort SreAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort SreAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort SreZeropageCycle5()
    {
        return 0;
    }
    private ushort SreZeropageCycle4()
    {
        return 0;
    }
    private ushort SreZeropageCycle3()
    {
        return 0;
    }
    private ushort SreZeropageCycle2()
    {
        return 0;
    }
    private ushort SreZeropageCycle1()
    {
        return 0;
    }

    private ushort SreZeropageXCycle6()
    {
        return 0;
    }
    private ushort SreZeropageXCycle5()
    {
        return 0;
    }
    private ushort SreZeropageXCycle4()
    {
        return 0;
    }
    private ushort SreZeropageXCycle3()
    {
        return 0;
    }
    private ushort SreZeropageXCycle2()
    {
        return 0;
    }
    private ushort SreZeropageXCycle1()
    {
        return 0;
    }


    private ushort SreIndirectXCycle8()
    {
        return 0;
    }
    private ushort SreIndirectXCycle7()
    {
        return 0;
    }
    private ushort SreIndirectXCycle6()
    {
        return 0;
    }
    private ushort SreIndirectXCycle5()
    {
        return 0;
    }
    private ushort SreIndirectXCycle4()
    {
        return 0;
    }
    private ushort SreIndirectXCycle3()
    {
        return 0;
    }
    private ushort SreIndirectXCycle2()
    {
        return 0;
    }
    private ushort SreIndirectXCycle1()
    {
        return 0;
    }

    private ushort SreIndirectYCycle8()
    {
        return 0;
    }
    private ushort SreIndirectYCycle7()
    {
        return 0;
    }
    private ushort SreIndirectYCycle6()
    {
        return 0;
    }
    private ushort SreIndirectYCycle5()
    {
        return 0;
    }
    private ushort SreIndirectYCycle4()
    {
        return 0;
    }
    private ushort SreIndirectYCycle3()
    {
        return 0;
    }
    private ushort SreIndirectYCycle2()
    {
        return 0;
    }
    private ushort SreIndirectYCycle1()
    {
        return 0;
    }

    private ushort LsrAccumCycle2()
    {
        return 0;
    }
    private ushort LsrAccumCycle1()
    {
        return 0;
    }

    private ushort LsrZeropageCycle5()
    {
        return 0;
    }
    private ushort LsrZeropageCycle4()
    {
        return 0;
    }
    private ushort LsrZeropageCycle3()
    {
        return 0;
    }
    private ushort LsrZeropageCycle2()
    {
        return 0;
    }
    private ushort LsrZeropageCycle1()
    {
        return 0;
    }

    private ushort LsrZeropageXCycle6()
    {
        return 0;
    }
    private ushort LsrZeropageXCycle5()
    {
        return 0;
    }
    private ushort LsrZeropageXCycle4()
    {
        return 0;
    }
    private ushort LsrZeropageXCycle3()
    {
        return 0;
    }
    private ushort LsrZeropageXCycle2()
    {
        return 0;
    }
    private ushort LsrZeropageXCycle1()
    {
        return 0;
    }

    private ushort LsrAbsoluteCycle6()
    {
        return 0;
    }
    private ushort LsrAbsoluteCycle5()
    {
        return 0;
    }
    private ushort LsrAbsoluteCycle4()
    {
        return 0;
    }
    private ushort LsrAbsoluteCycle3()
    {
        return 0;
    }
    private ushort LsrAbsoluteCycle2()
    {
        return 0;
    }
    private ushort LsrAbsoluteCycle1()
    {
        return 0;
    }

    private ushort LsrAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort LsrAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort RraAbsoluteCycle6()
    {
        return 0;
    }
    private ushort RraAbsoluteCycle5()
    {
        return 0;
    }
    private ushort RraAbsoluteCycle4()
    {
        return 0;
    }
    private ushort RraAbsoluteCycle3()
    {
        return 0;
    }
    private ushort RraAbsoluteCycle2()
    {
        return 0;
    }
    private ushort RraAbsoluteCycle1()
    {
        return 0;
    }

    private ushort RraAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort RraAbsoluteXCycle1()
    {
        return 0;
    }

    private ushort RraAbsoluteYCycle7()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle6()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle5()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle4()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle3()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle2()
    {
        return 0;
    }
    private ushort RraAbsoluteYCycle1()
    {
        return 0;
    }

    private ushort RraZeropageCycle5()
    {
        return 0;
    }
    private ushort RraZeropageCycle4()
    {
        return 0;
    }
    private ushort RraZeropageCycle3()
    {
        return 0;
    }
    private ushort RraZeropageCycle2()
    {
        return 0;
    }
    private ushort RraZeropageCycle1()
    {
        return 0;
    }

    private ushort RraZeropageXCycle6()
    {
        return 0;
    }
    private ushort RraZeropageXCycle5()
    {
        return 0;
    }
    private ushort RraZeropageXCycle4()
    {
        return 0;
    }
    private ushort RraZeropageXCycle3()
    {
        return 0;
    }
    private ushort RraZeropageXCycle2()
    {
        return 0;
    }
    private ushort RraZeropageXCycle1()
    {
        return 0;
    }

    private ushort RraIndirectXCycle8()
    {
        return 0;
    }
    private ushort RraIndirectXCycle7()
    {
        return 0;
    }

    private ushort RraIndirectXCycle6()
    {
        return 0;
    }
    private ushort RraIndirectXCycle5()
    {
        return 0;
    }
    private ushort RraIndirectXCycle4()
    {
        return 0;
    }
    private ushort RraIndirectXCycle3()
    {
        return 0;
    }
    private ushort RraIndirectXCycle2()
    {
        return 0;
    }
    private ushort RraIndirectXCycle1()
    {
        return 0;
    }

    private ushort RraIndirectYCycle8()
    {
        return 0;
    }
    private ushort RraIndirectYCycle7()
    {
        return 0;
    }

    private ushort RraIndirectYCycle6()
    {
        return 0;
    }
    private ushort RraIndirectYCycle5()
    {
        return 0;
    }
    private ushort RraIndirectYCycle4()
    {
        return 0;
    }
    private ushort RraIndirectYCycle3()
    {
        return 0;
    }
    private ushort RraIndirectYCycle2()
    {
        return 0;
    }
    private ushort RraIndirectYCycle1()
    {
        return 0;
    }

    private ushort JmpAbsoluteCycle4()
    {
        return 0;
    }
    private ushort JmpAbsoluteCycle3()
    {
        return 0;
    }
    private ushort JmpAbsoluteCycle2()
    {
        return 0;
    }
    private ushort JmpAbsoluteCycle1()
    {
        return 0;
    }

    private ushort JmpIndirectCycle5()
    {
        return 0;
    }
    private ushort JmpIndirectCycle4()
    {
        return 0;
    }
    private ushort JmpIndirectCycle3()
    {
        return 0;
    }
    private ushort JmpIndirectCycle2()
    {
        return 0;
    }
    private ushort JmpIndirectCycle1()
    {
        return 0;
    }

    private ushort SaxAbsoluteCycle4()
    {
        return 0;
    }
    private ushort SaxAbsoluteCycle3()
    {
        return 0;
    }
    private ushort SaxAbsoluteCycle2()
    {
        return 0;
    }
    private ushort SaxAbsoluteCycle1()
    {
        return 0;
    }


    private ushort SaxZeropageCycle3()
    {
        return 0;
    }
    private ushort SaxZeropageCycle2()
    {
        return 0;
    }
    private ushort SaxZeropageCycle1()
    {
        return 0;
    }

    private ushort SaxZeropageYCycle6()
    {
        return 0;
    }
    private ushort SaxZeropageYCycle5()
    {
        return 0;
    }
    private ushort SaxZeropageYCycle4()
    {
        return 0;
    }
    private ushort SaxZeropageYCycle3()
    {
        return 0;
    }
    private ushort SaxZeropageYCycle2()
    {
        return 0;
    }
    private ushort SaxZeropageYCycle1()
    {
        return 0;
    }

    private ushort SaxIndirectXCycle6()
    {
        return 0;
    }
    private ushort SaxIndirectXCycle5()
    {
        return 0;
    }
    private ushort SaxIndirectXCycle4()
    {
        return 0;
    }
    private ushort SaxIndirectXCycle3()
    {
        return 0;
    }
    private ushort SaxIndirectXCycle2()
    {
        return 0;
    }
    private ushort SaxIndirectXCycle1()
    {
        return 0;
    }

    private ushort StxZeropageCycle3()
    {
        return 0;
    }
    private ushort StxZeropageCycle2()
    {
        return 0;
    }
    private ushort StxZeropageCycle1()
    {
        return 0;
    }

    private ushort StxZeropageYCycle4()
    {
        return 0;
    }
    private ushort StxZeropageYCycle3()
    {
        return 0;
    }
    private ushort StxZeropageYCycle2()
    {
        return 0;
    }
    private ushort StxZeropageYCycle1()
    {
        return 0;
    }

    private ushort StxAbsoluteCycle4()
    {
        return 0;
    }
    private ushort StxAbsoluteCycle3()
    {
        return 0;
    }
    private ushort StxAbsoluteCycle2()
    {
        return 0;
    }
    private ushort StxAbsoluteCycle1()
    {
        return 0;
    }

    // DEC
    private ushort DecZeropageCycle5()
    {
        return 0;
    }
    private ushort DecZeropageCycle4()
    {
        return 0;
    }
    private ushort DecZeropageCycle3()
    {
        return 0;
    }
    private ushort DecZeropageCycle2()
    {
        return 0;
    }
    private ushort DecZeropageCycle1()
    {
        return 0;
    }


    private ushort DecZeropageXCycle6()
    {
        return 0;
    }
    private ushort DecZeropageXCycle5()
    {
        return 0;
    }
    private ushort DecZeropageXCycle4()
    {
        return 0;
    }
    private ushort DecZeropageXCycle3()
    {
        return 0;
    }
    private ushort DecZeropageXCycle2()
    {
        return 0;
    }
    private ushort DecZeropageXCycle1()
    {
        return 0;
    }

    private ushort DecAbsoluteCycle6()
    {
        return 0;
    }
    private ushort DecAbsoluteCycle5()
    {
        return 0;
    }
    private ushort DecAbsoluteCycle4()
    {
        return 0;
    }
    private ushort DecAbsoluteCycle3()
    {
        return 0;
    }
    private ushort DecAbsoluteCycle2()
    {
        return 0;
    }
    private ushort DecAbsoluteCycle1()
    {
        return 0;
    }

    private ushort DecAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort DecAbsoluteXCycle1()
    {
        return 0;
    }

    // INC
    private ushort IncZeropageCycle5()
    {
        return 0;
    }
    private ushort IncZeropageCycle4()
    {
        return 0;
    }
    private ushort IncZeropageCycle3()
    {
        return 0;
    }
    private ushort IncZeropageCycle2()
    {
        return 0;
    }
    private ushort IncZeropageCycle1()
    {
        return 0;
    }


    private ushort IncZeropageXCycle6()
    {
        return 0;
    }
    private ushort IncZeropageXCycle5()
    {
        return 0;
    }
    private ushort IncZeropageXCycle4()
    {
        return 0;
    }
    private ushort IncZeropageXCycle3()
    {
        return 0;
    }
    private ushort IncZeropageXCycle2()
    {
        return 0;
    }
    private ushort IncZeropageXCycle1()
    {
        return 0;
    }

    private ushort IncAbsoluteCycle6()
    {
        return 0;
    }
    private ushort IncAbsoluteCycle5()
    {
        return 0;
    }
    private ushort IncAbsoluteCycle4()
    {
        return 0;
    }
    private ushort IncAbsoluteCycle3()
    {
        return 0;
    }
    private ushort IncAbsoluteCycle2()
    {
        return 0;
    }
    private ushort IncAbsoluteCycle1()
    {
        return 0;
    }

    private ushort IncAbsoluteXCycle7()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle6()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle5()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle4()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle3()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle2()
    {
        return 0;
    }
    private ushort IncAbsoluteXCycle1()
    {
        return 0;
    }
}