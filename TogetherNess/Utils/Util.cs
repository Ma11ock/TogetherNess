namespace TogetherNess.Utils;

public static class Util
{
    public static byte SetBitValue(this byte b, int nthBit, sbyte @value)
        => (byte)(b ^ (-@value ^ b) & (1u << nthBit));

    public static byte SetBitValue(this byte b, int nthBit, bool @value) => b.SetBitValue(nthBit, (sbyte)(@value ? 1 : 0));

    public static byte SetBit(this byte b, int nthBit) => b.SetBitValue(nthBit, true);

    public static byte ClearBit(this byte b, int nthBit) => b.SetBitValue(nthBit, false);

    public static int Kilobytes(this int l) => l * 1024;

    public static uint Kilobytes(this uint l) => l * 1024;

    public static long Kilobytes(this long l) => l * 1024;

    public static ulong Kilobytes(this ulong l) => l * 1024;

    public static byte GetBit(this byte b, int nthBit) => (byte)(b & (1u << nthBit));
}