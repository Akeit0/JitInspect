using Iced.Intel;

namespace JitInspect;

internal class AsmSymbolResolver(
    HashSet<ulong> symbols,
    ulong currentMethodAddress,
    uint currentMethodLength)
    : ISymbolResolver
{
    public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize, out SymbolResult symbol)
    {
        if (address >= currentMethodAddress && address < currentMethodAddress + currentMethodLength)
        {
            // relative offset reference
            symbol = new(address, "L" + (address - currentMethodAddress).ToString("x4"));
            symbols.Add(address - currentMethodAddress);
            return true;
        }

        symbol = default;
        return false;
    }
}