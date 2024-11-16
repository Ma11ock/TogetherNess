using System.Net.Sockets;

namespace TogetherNess.Hardware;

public class NesMemory : IMemory
{
    private readonly byte[] _ram = new byte[0x800];

    public NesMemory()
    {
    }

    public int this[int address]
    {
        get => Load(address);
        set => Store(address, value);
    }

    public int Load(int address) => (ushort)address switch
    {
        > 0 and < 0x800 => _ram[address],
        _ => 0
    };

    public int Store(int address, int value) => (ushort)address switch
    {
        > 0 and < 0x800 => _ram[address] = (byte)value,
        _ => 0
    };
}