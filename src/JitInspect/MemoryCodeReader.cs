using System.Runtime.CompilerServices;
using Iced.Intel;

namespace JitInspect;

internal class MemoryCodeReader(IntPtr startPointer, uint length) : CodeReader
{
    uint offset;

    public override unsafe int ReadByte()
    {
        if (offset >= length)
            return -1;

        var @byte = Unsafe.Read<byte>((startPointer + (int)offset).ToPointer());
        offset += 1;
        return @byte;
    }
}