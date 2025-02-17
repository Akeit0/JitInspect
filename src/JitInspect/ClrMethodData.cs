namespace JitInspect;

internal readonly struct ClrMethodData(ulong methodAddress, uint methodSize)
{
    public ulong MethodAddress { get; } = methodAddress;
    public uint MethodSize { get; } = methodSize;
}