namespace TogetherNess.Hardware;

public interface IMemory
{
    int this[int address] { get; set; }
}