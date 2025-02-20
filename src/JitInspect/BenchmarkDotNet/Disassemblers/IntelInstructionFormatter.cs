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
using Iced.Intel;

namespace BenchmarkDotNet.Disassemblers
{
    internal static class IntelInstructionFormatter
    {
        internal static string Format(Instruction instruction, Formatter formatter, bool printInstructionAddresses, uint pointerSize)
        {
            var output = new StringOutput();

            if (printInstructionAddresses)
            {
                FormatInstructionPointer(instruction, formatter, pointerSize, output);
            }

            formatter.Format(instruction, output);

            return output.ToString();
        }

        private static void FormatInstructionPointer(Instruction instruction, Formatter formatter, uint pointerSize, StringOutput output)
        {
            string ipFormat = formatter.Options.LeadingZeroes
                ? pointerSize == 4 ? "X8" : "X16"
                : "X";

            output.Write(instruction.IP.ToString(ipFormat), FormatterTextKind.Text);
            output.Write(" ", FormatterTextKind.Text);
        }
    }
}