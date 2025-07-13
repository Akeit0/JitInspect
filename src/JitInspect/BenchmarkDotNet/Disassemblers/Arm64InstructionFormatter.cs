/*
Copyright (c) 2013–2024 .NET Foundation and contributors

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Gee.External.Capstone.Arm64;
using Iced.Intel;
using System.Collections.Generic;
using System.Text;

namespace BenchmarkDotNet.Disassemblers;

internal static class Arm64InstructionFormatter
{
    // FormatterOptions is an Intel-specific concept that comes from the Iced library, but since our users can pass custom
    // Iced Formatter to DisassemblyDiagnoserConfig and it provides all the settings we need, we just reuse it here.
    internal static string Format(Arm64Asm asm, FormatterOptions formatterOptions,
        bool printInstructionAddresses, uint pointerSize, IReadOnlyDictionary<ulong, string> symbols)
    {
        StringBuilder output = new();
        var instruction = asm.Instruction;

        if (printInstructionAddresses) FormatInstructionPointer(instruction, formatterOptions, pointerSize, output);

        output.Append(instruction.Mnemonic.ToString().PadRight(formatterOptions.FirstOperandCharIndex));

        if (asm.ReferencedAddress.HasValue && !asm.IsReferencedAddressIndirect && symbols.TryGetValue(asm.ReferencedAddress.Value, out var name))
        {
            var partToReplace = $"#0x{asm.ReferencedAddress.Value:x}";
            output.Append(instruction.Operand.Replace(partToReplace, name));
        }
        else
        {
            output.Append(instruction.Operand);
        }

        return output.ToString();
    }

    static void FormatInstructionPointer(Arm64Instruction instruction, FormatterOptions formatterOptions, uint pointerSize, StringBuilder output)
    {
        var ipFormat = formatterOptions.LeadingZeroes
            ? pointerSize == 4 ? "X8" : "X16"
            : "X";

        output.Append(instruction.Address.ToString(ipFormat));
        output.Append(' ');
    }
}