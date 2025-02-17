using Iced.Intel;
using Microsoft.Diagnostics.Runtime;

namespace JitInspect;

internal class AsmSymbolResolver(
    ClrRuntime runtime,
    ulong currentMethodAddress,
    uint currentMethodLength)
    : ISymbolResolver
{
    public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize, out SymbolResult symbol)
    {
        if (address >= currentMethodAddress && address < currentMethodAddress + currentMethodLength)
        {
            // relative offset reference
            symbol = new SymbolResult(address, "L" + (address - currentMethodAddress).ToString("x4"));
            return true;
        }

        ClrMethod? clrMethod = null;
        if (address is > 1024 and < 0xFFFFFFFFFFFFFFFF)
        {
            clrMethod = runtime.GetMethodByInstructionPointer(address);
        }

        if (clrMethod is null)
        {
            symbol = default;
            return false;
        }

        symbol = new SymbolResult(address, $"0x{address:x} : {clrMethod.Signature}");
        return true;
    }
}