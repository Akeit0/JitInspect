﻿/*
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

using BenchmarkDotNet.Diagnosers;
using Iced.Intel;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BenchmarkDotNet.Disassemblers;

internal partial class IntelDisassembler : ClrMdV3Disassembler
{
    internal sealed class RuntimeSpecificData
    {
        // See dotnet/runtime src/coreclr/vm/amd64/thunktemplates.asm/.S for the stub code
        // mov    rax,QWORD PTR [rip + DATA_SLOT(CallCountingStub, RemainingCallCountCell)]
        // dec    WORD PTR [rax]
        // je     LOCAL_LABEL(CountReachedZero)
        // jmp    QWORD PTR [rip + DATA_SLOT(CallCountingStub, TargetForMethod)]
        // LOCAL_LABEL(CountReachedZero):
        // jmp    QWORD PTR [rip + DATA_SLOT(CallCountingStub, TargetForThresholdReached)]
        internal readonly byte[] callCountingStubTemplate = new byte[10] { 0x48, 0x8b, 0x05, 0xf9, 0x0f, 0x00, 0x00, 0x66, 0xff, 0x08 };

        // mov    r10, [rip + DATA_SLOT(StubPrecode, MethodDesc)]
        // jmp    [rip + DATA_SLOT(StubPrecode, Target)]
        internal readonly byte[] stubPrecodeTemplate = new byte[13] { 0x4c, 0x8b, 0x15, 0xf9, 0x0f, 0x00, 0x00, 0xff, 0x25, 0xfb, 0x0f, 0x00, 0x00 };

        // jmp    [rip + DATA_SLOT(FixupPrecode, Target)]
        // mov    r10, [rip + DATA_SLOT(FixupPrecode, MethodDesc)]
        // jmp    [rip + DATA_SLOT(FixupPrecode, PrecodeFixupThunk)]
        internal readonly byte[] fixupPrecodeTemplate = new byte[19] { 0xff, 0x25, 0xfa, 0x0f, 0x00, 0x00, 0x4c, 0x8b, 0x15, 0xfb, 0x0f, 0x00, 0x00, 0xff, 0x25, 0xfd, 0x0f, 0x00, 0x00 };
        internal readonly ulong stubPageSize;

        internal RuntimeSpecificData(State state)
        {
            stubPageSize = (ulong)Environment.SystemPageSize;
            if (state.RuntimeVersion.Major >= 8)
            {
                // In .NET 8, the stub page size was changed to 16kB
                stubPageSize = 16384;
                // Update the templates so that the offsets are correct
                callCountingStubTemplate[4] = 0x3f;
                stubPrecodeTemplate[4] = 0x3f;
                stubPrecodeTemplate[10] = 0x3f;
                fixupPrecodeTemplate[3] = 0x3f;
                fixupPrecodeTemplate[10] = 0x3f;
                fixupPrecodeTemplate[16] = 0x3f;
            }
        }
    }

    static readonly Dictionary<Version, RuntimeSpecificData> runtimeSpecificData = new();

    protected override IEnumerable<Asm> Decode(byte[] code, ulong startAddress, State state, int depth, ClrMethod currentMethod, DisassemblySyntax syntax)
    {
        if (!runtimeSpecificData.TryGetValue(state.RuntimeVersion, out var data)) runtimeSpecificData.Add(state.RuntimeVersion, data = new(state));

        var reader = new ByteArrayCodeReader(code);
        var decoder = Decoder.Create(state.Runtime.DataTarget.DataReader.PointerSize * 8, reader);
        decoder.IP = startAddress;

        while (reader.CanReadByte)
        {
            decoder.Decode(out var instruction);

            var isIndirect = instruction.IsCallFarIndirect || instruction.IsCallNearIndirect || instruction.IsJmpFarIndirect || instruction.IsJmpNearIndirect;
            var isPrestubMD = false;

            ulong address = 0;
            if (TryGetReferencedAddress(instruction, (uint)state.Runtime.DataTarget.DataReader.PointerSize, out address))
            {
                if (isIndirect)
                {
                    address = state.Runtime.DataTarget.DataReader.ReadPointer(address);
                    if (state.RuntimeVersion.Major >= 7)
                    {
                        // Check if the target is a known stub
                        // The stubs are allocated in interleaved code / data pages in memory. The data part of the stub
                        // is at an address one memory page higher than the code.
                        var buffer = new byte[10];

                        FlushCachedDataIfNeeded(state.Runtime.DataTarget.DataReader, address, buffer);

                        if (state.Runtime.DataTarget.DataReader.Read(address, buffer) == buffer.Length && buffer.SequenceEqual(data.callCountingStubTemplate))
                        {
                            const ulong TargetMethodAddressSlotOffset = 8;
                            address = state.Runtime.DataTarget.DataReader.ReadPointer(address + data.stubPageSize + TargetMethodAddressSlotOffset);
                        }
                        else
                        {
                            buffer = new byte[13];
                            if (state.Runtime.DataTarget.DataReader.Read(address, buffer) == buffer.Length && buffer.SequenceEqual(data.stubPrecodeTemplate))
                            {
                                const ulong MethodDescSlotOffset = 0;
                                address = state.Runtime.DataTarget.DataReader.ReadPointer(address + data.stubPageSize + MethodDescSlotOffset);
                                isPrestubMD = true;
                            }
                            else
                            {
                                buffer = new byte[19];
                                if (state.Runtime.DataTarget.DataReader.Read(address, buffer) == buffer.Length && buffer.SequenceEqual(data.fixupPrecodeTemplate))
                                {
                                    const ulong MethodDescSlotOffset = 8;
                                    address = state.Runtime.DataTarget.DataReader.ReadPointer(address + data.stubPageSize + MethodDescSlotOffset);
                                    isPrestubMD = true;
                                }
                            }
                        }
                    }
                }

                TryTranslateAddressToName(address, isPrestubMD, state, depth, currentMethod);
            }

            yield return new IntelAsm
            {
                InstructionPointer = instruction.IP,
                InstructionLength = instruction.Length,
                Instruction = instruction,
                ReferencedAddress = address > ushort.MaxValue ? address : null,
                IsReferencedAddressIndirect = isIndirect
            };
        }
    }

    static bool TryGetReferencedAddress(Instruction instruction, uint pointerSize, out ulong referencedAddress)
    {
        for (var i = 0; i < instruction.OpCount; i++)
            switch (instruction.GetOpKind(i))
            {
                case OpKind.NearBranch16:
                case OpKind.NearBranch32:
                case OpKind.NearBranch64:
                    referencedAddress = instruction.NearBranchTarget;
                    return referencedAddress > ushort.MaxValue;
                case OpKind.Immediate16:
                case OpKind.Immediate8to16:
                case OpKind.Immediate8to32:
                case OpKind.Immediate8to64:
                case OpKind.Immediate32to64:
                case OpKind.Immediate32 when pointerSize == 4:
                case OpKind.Immediate64:
                    referencedAddress = instruction.GetImmediate(i);
                    return referencedAddress > ushort.MaxValue;
                case OpKind.Memory when instruction.IsIPRelativeMemoryOperand:
                    referencedAddress = instruction.IPRelativeMemoryAddress;
                    return referencedAddress > ushort.MaxValue;
                case OpKind.Memory:
                    referencedAddress = instruction.MemoryDisplacement64;
                    return referencedAddress > ushort.MaxValue;
            }

        referencedAddress = default;
        return false;
    }
}