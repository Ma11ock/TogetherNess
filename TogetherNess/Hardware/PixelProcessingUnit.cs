namespace TogetherNess.Hardware;

public class PixelProcessingUnit
{
    public byte Control { get; } = 0;

    public byte Mask { get; } = 0;

    public byte Status { get; } = 0;

    public byte OamAddr { get; } = 0;

    public byte OamData { get; } = 0;

    public byte Scroll { get; } = 0;

    public byte Addr { get; } = 0;

    public byte Data { get; } = 0;
 
    public byte OamDma { get; } = 0;
}