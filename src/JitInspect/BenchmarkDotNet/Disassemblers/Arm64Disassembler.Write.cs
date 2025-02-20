using System.Buffers;
using Iced.Intel;
using JitInspect;

namespace BenchmarkDotNet.Disassemblers;

partial class Arm64Disassembler
{
    static readonly FormatterOptions formatterOptions = new()
    {
        HexPrefix = "0x",
        HexSuffix = null,
        UppercaseHex = false,
        SpaceAfterOperandSeparator = true
    };

    private protected override void Write(IBufferWriter<char> writer, State state, IEnumerable<Asm> asms, ulong methodAddress, uint methodLength)
    {
        foreach (var asm in asms)
        {
            var intelAsm = (Arm64Asm)asm;
            var instruction = intelAsm.Instruction;

            writer.Write("L");
            writer.Write(((ulong)instruction.Address - methodAddress).ToString("x4"));
            writer.Write(": ");
            writer.Write(instruction.Mnemonic.ToString().PadRight(formatterOptions.FirstOperandCharIndex));
            var referencedAddress = intelAsm.ReferencedAddress;
             if (referencedAddress.HasValue && state.AddressToNameMapping.TryGetValue(referencedAddress.Value, out var name))
            {
                writer.Write("; ");
                writer.WriteLine(name);
            }
            else
            {
                writer.WriteLine();
            }
        }
    }
}