using Iced.Intel;

namespace JitInspect;

internal class AsmSymbolResolver(
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

        symbol = default;
        return false;
    }
}