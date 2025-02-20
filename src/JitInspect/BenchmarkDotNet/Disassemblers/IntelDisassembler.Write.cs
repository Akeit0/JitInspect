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
using System.Buffers;
using Iced.Intel;
using JitInspect;
using Microsoft.Diagnostics.Runtime;

namespace BenchmarkDotNet.Disassemblers;

partial class IntelDisassembler
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
        AsmSymbolResolver resolver = new(methodAddress, methodLength);
        var formatter = new IntelFormatter(formatterOptions, resolver);
        var output = new DirectFormatterOutput(writer);
        foreach (var asm in asms)
        {
            var intelAsm = (IntelAsm)asm;
            var instruction = intelAsm.Instruction;

            writer.Write("L");
            writer.Write((instruction.IP - methodAddress).ToString("x4"));
            writer.Write(": ");
            formatter.Format(instruction, output);
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