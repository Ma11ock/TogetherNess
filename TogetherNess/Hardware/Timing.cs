using System;

namespace TogetherNess.Hardware;

[Flags]
public enum Timing
{
    Nothing = 0,
    T0 = 1 << 0,
    TPlus = 1 << 1,
    T1 = 1 << 2,
    T2 = 1 << 3,
    T3 = 1 << 4,
    T4 = 1 << 5,
    T5 = 1 << 6,
    V0 = 1 << 7,
    T6 = 1 << 8,
    SD1 = 1 << 9,
    SD2 = 1 << 10,
}