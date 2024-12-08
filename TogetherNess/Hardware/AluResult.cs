namespace TogetherNess.Hardware;

public readonly record struct AluResult(
    byte Result,
    bool Carry,
    bool Overflow,
    bool Zero,
    bool Negative
    );