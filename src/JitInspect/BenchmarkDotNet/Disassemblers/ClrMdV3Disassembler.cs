using BenchmarkDotNet.Diagnosers;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Detectors;
using BenchmarkDotNet.Filters;

namespace BenchmarkDotNet.Disassemblers;

// This Disassembler uses ClrMd v3x. Please keep it in sync with ClrMdV1Disassembler (if possible).
internal abstract class ClrMdV3Disassembler

{
    static readonly ulong MinValidAddress = GetMinValidAddress();

    static ulong GetMinValidAddress()
    {
        // https://github.com/dotnet/BenchmarkDotNet/pull/2413#issuecomment-1688100117
        if (OsDetector.IsWindows())
            return ushort.MaxValue + 1;
        if (OsDetector.IsLinux())
            return (ulong)Environment.SystemPageSize;
        if (OsDetector.IsMacOS())
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X86 or Architecture.X64 => 4096,
                Architecture.Arm64 => 0x100000000,
                _ => throw new NotSupportedException($"{RuntimeInformation.OSArchitecture} is not supported")
            };
        throw new NotSupportedException($"{RuntimeInformation.OSDescription} is not supported");
    }

    static bool IsValidAddress(ulong address) // -1 (ulong.MaxValue) address is invalid, and will crash the runtime in older runtimes. https://github.com/dotnet/runtime/pull/90794
    // 0 is NULL and therefore never valid.
    // Addresses less than the minimum virtual address are also invalid.
    {
    return address != ulong.MaxValue
           && address != 0
           && address >= MinValidAddress;
    }

    internal DisassemblyResult AttachAndDisassemble(Settings settings)
    {
        using (var dataTarget = DataTarget.AttachToProcess(
                   settings.ProcessId,
                   false))
        {
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();

            ConfigureSymbols(dataTarget);

            var state = new State(runtime, settings.TargetFrameworkMoniker);

            if (settings.Filters.Length > 0)
            {
                FilterAndEnqueue(state, settings);
            }
            else
            {
                var typeWithBenchmark = state.Runtime.EnumerateModules().Select(module => module.GetTypeByName(settings.TypeName)).First(type => type != null);

                state.Todo.Enqueue(
                    new(
                        // the Disassembler Entry Method is always parameterless, so check by name is enough
                        typeWithBenchmark.Methods.Single(method => method.Attributes.HasFlag(System.Reflection.MethodAttributes.Public) && method.Name == settings.MethodName),
                        0));
            }

            var disassembledMethods = Disassemble(settings, state);

            // we don't want to export the disassembler entry point method which is just an artificial method added to get generic types working
            var filteredMethods = disassembledMethods.Length == 1
                ? disassembledMethods // if there is only one method we want to return it (most probably benchmark got inlined)
                : disassembledMethods.Where(method => !method.Name.Contains(DisassemblerConstants.DisassemblerEntryMethodName)).ToArray();

            return new()
            {
                Methods = filteredMethods,
                SerializedAddressToNameMapping = state.AddressToNameMapping.Select(x => new DisassemblyResult.MutablePair { Key = x.Key, Value = x.Value }).ToArray(),
                PointerSize = (uint)IntPtr.Size
            };
        }
    }

    static void ConfigureSymbols(DataTarget dataTarget)
    {
        // code copied from https://github.com/Microsoft/clrmd/issues/34#issuecomment-161926535
        dataTarget.SetSymbolPath("http://msdl.microsoft.com/download/symbols");
    }

    static void FilterAndEnqueue(State state, Settings settings)
    {
        var filters = GlobFilter.ToRegex(settings.Filters);

        foreach (var module in state.Runtime.EnumerateModules())
        foreach (var type in module.EnumerateTypeDefToMethodTableMap().Select(map => state.Runtime.GetTypeByMethodTable(map.MethodTable)).Where(type => type is not null))
        foreach (var method in type.Methods.Where(method => method.Signature != null))
        {
            if (method.NativeCode > 0)
                if (!state.AddressToNameMapping.TryGetValue(method.NativeCode, out _))
                    state.AddressToNameMapping.Add(method.NativeCode, method.Signature);

            if (CanBeDisassembled(method))
                foreach (var filter in filters)
                    if (filter.IsMatch(method.Signature))
                    {
                        state.Todo.Enqueue(new(method,
                            settings.MaxDepth)); // don't allow for recursive disassembling
                        break;
                    }
        }
    }

    DisassembledMethod[] Disassemble(Settings settings, State state)
    {
        var result = new List<DisassembledMethod>();
        var syntax = (DisassemblySyntax)Enum.Parse(typeof(DisassemblySyntax), settings.Syntax);

        using var sourceCodeProvider = new SourceCodeProvider();
        while (state.Todo.Count != 0)
        {
            var methodInfo = state.Todo.Dequeue();

            if (!state.HandledMethods.Add(methodInfo.Method)) // add it now to avoid StackOverflow for recursive methods
                continue; // already handled

            if (settings.MaxDepth >= methodInfo.Depth)
                result.Add(DisassembleMethod(methodInfo, state, settings, syntax, sourceCodeProvider));
        }

        return result.ToArray();
    }

    static bool CanBeDisassembled(ClrMethod method)
    {
        return method.ILOffsetMap.Length > 0 && method.NativeCode > 0;
    }

    internal DisassembledMethod DisassembleMethod(MethodInfo methodInfo, State state, Settings settings, DisassemblySyntax syntax, SourceCodeProvider sourceCodeProvider)
    {
        var method = methodInfo.Method;

        if (!CanBeDisassembled(method))
        {
            if (method.Attributes.HasFlag(System.Reflection.MethodAttributes.PinvokeImpl))
                return CreateEmpty(method, "PInvoke method");
            var ilInfo = method.GetILInfo();
            if (ilInfo is null || ilInfo.Length == 0)
                return CreateEmpty(method, "Extern method");
            if (method.CompilationType == MethodCompilationType.None)
                return CreateEmpty(method, "Method was not JITted yet.");

            return CreateEmpty(method, $"No valid {nameof(method.ILOffsetMap)} and {nameof(method.HotColdInfo)}");
        }

        var codes = new List<SourceCode>();
        if (settings.PrintSource && method.ILOffsetMap.Length > 0)
        {
            // we use HashSet to prevent from duplicates
            var uniqueSourceCodeLines = new HashSet<Sharp>(new SharpComparer());
            // for getting C# code we always use the original ILOffsetMap
            foreach (var map in method.ILOffsetMap.Where(map => map.StartAddress < map.EndAddress && map.ILOffset >= 0).OrderBy(map => map.StartAddress))
            foreach (var sharp in sourceCodeProvider.GetSource(method, map))
                uniqueSourceCodeLines.Add(sharp);

            codes.AddRange(uniqueSourceCodeLines);
        }

        foreach (var map in GetCompleteNativeMap(method, state.Runtime)) codes.AddRange(Decode(map, state, methodInfo.Depth, method, syntax));

        var maps = settings.PrintSource
            ? codes.GroupBy(code => code.InstructionPointer).OrderBy(group => group.Key).Select(group => new Map { SourceCodes = group.ToArray() }).ToArray()
            : new[] { new Map { SourceCodes = codes.ToArray() } };

        return new()
        {
            Maps = maps,
            Name = method.Signature,
            NativeCode = method.NativeCode
        };
    }

    IEnumerable<Asm> Decode(ILToNativeMap map, State state, int depth, ClrMethod currentMethod, DisassemblySyntax syntax)
    {
        var startAddress = map.StartAddress;
        var size = (uint)(map.EndAddress - map.StartAddress);

        var code = new byte[size];

        var totalBytesRead = 0;
        do
        {
            var bytesRead = state.Runtime.DataTarget.DataReader.Read(startAddress + (ulong)totalBytesRead, new(code, totalBytesRead, (int)size - totalBytesRead));
            if (bytesRead <= 0) throw new EndOfStreamException($"Tried to read {size} bytes for {currentMethod.Signature}, got only {totalBytesRead}");

            totalBytesRead += bytesRead;
        } while (totalBytesRead != size);

        return Decode(code, startAddress, state, depth, currentMethod, syntax);
    }

    protected abstract IEnumerable<Asm> Decode(byte[] code, ulong startAddress, State state, int depth, ClrMethod currentMethod, DisassemblySyntax syntax);

    static ILToNativeMap[] GetCompleteNativeMap(ClrMethod method, ClrRuntime runtime)
    {
        // it's better to use one single map rather than few small ones
        // it's simply easier to get next instruction when decoding ;)

        var hotColdInfo = method.HotColdInfo;
        if (hotColdInfo.HotSize > 0 && hotColdInfo.HotStart > 0)
            return hotColdInfo.ColdSize <= 0
                ? new[] { new ILToNativeMap { StartAddress = hotColdInfo.HotStart, EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1 } }
                : new[]
                {
                    new ILToNativeMap { StartAddress = hotColdInfo.HotStart, EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1 },
                    new ILToNativeMap { StartAddress = hotColdInfo.ColdStart, EndAddress = hotColdInfo.ColdStart + hotColdInfo.ColdSize, ILOffset = -1 }
                };

        return method.ILOffsetMap
            .Where(map => map.StartAddress < map.EndAddress) // some maps have 0 length?
            .OrderBy(map => map.StartAddress) // we need to print in the machine code order, not IL! #536
            .ToArray();
    }

    static DisassembledMethod CreateEmpty(ClrMethod method, string reason)
    {
        return DisassembledMethod.Empty(method.Signature, method.NativeCode, reason);
    }

    protected void TryTranslateAddressToName(ulong address, bool isAddressPrecodeMD, State state, int depth, ClrMethod currentMethod)
    {
        if (!IsValidAddress(address) || state.AddressToNameMapping.ContainsKey(address))
            return;

        var runtime = state.Runtime;

        var jitHelperFunctionName = runtime.GetJitHelperFunctionName(address);
        if (!string.IsNullOrEmpty(jitHelperFunctionName))
        {
            state.AddressToNameMapping.Add(address, jitHelperFunctionName);
            return;
        }

        var method = runtime.GetMethodByInstructionPointer(address);
        if (method is null && (address & ((uint)runtime.DataTarget.DataReader.PointerSize - 1)) == 0
                           && runtime.DataTarget.DataReader.ReadPointer(address, out var newAddress) && IsValidAddress(newAddress))
            method = runtime.GetMethodByInstructionPointer(newAddress);

        if (method is null)
        {
            var methodDescriptor = runtime.GetMethodByHandle(address);
            if (methodDescriptor is not null)
            {
                if (isAddressPrecodeMD)
                    state.AddressToNameMapping.Add(address, $"Precode of {methodDescriptor.Signature}");
                else
                    state.AddressToNameMapping.Add(address, $"MD_{methodDescriptor.Signature}");

                return;
            }

            var methodTableName = runtime.GetTypeByMethodTable(address)?.Name;
            if (!string.IsNullOrEmpty(methodTableName)) state.AddressToNameMapping.Add(address, $"MT_{methodTableName}");

            return;
        }

        if (method.NativeCode == currentMethod.NativeCode && method.Signature == currentMethod.Signature)
            return; // in case of a call which is just a jump within the method or a recursive call

        if (!state.HandledMethods.Contains(method))
            state.Todo.Enqueue(new(method, depth + 1));

        var methodName = method.Signature;
        if (!methodName.Any(c => c == '.')) // the method name does not contain namespace and type name
            methodName = $"{method.Type.Name}.{method.Signature}";
        state.AddressToNameMapping.Add(address, methodName);
    }

    protected void FlushCachedDataIfNeeded(IDataReader dataTargetDataReader, ulong address, byte[] buffer)
    {
        if (!OsDetector.IsWindows())
            if (dataTargetDataReader.Read(address, buffer) <= 0)
                // We don't suspend the benchmark process for the time of disassembling,
                // as it would require sudo privileges.
                // Because of that, the Tiered JIT thread might still re-compile some methods
                // in the meantime when the host process it trying to disassemble the code.
                // In such case, Tiered JIT thread might create stubs which requires flushing of the cached data.
                dataTargetDataReader.FlushCachedData();
    }

    class SharpComparer : IEqualityComparer<Sharp>
    {
        public bool Equals(Sharp x, Sharp y)
        {
            // sometimes some C# code lines are duplicated because the same line is the best match for multiple ILToNativeMaps
            // we don't want to confuse the users, so this must also be removed
            return x.FilePath == y.FilePath && x.LineNumber == y.LineNumber;
        }

        public int GetHashCode(Sharp obj)
        {
            return HashCode.Combine(obj.FilePath, obj.LineNumber);
        }
    }
}