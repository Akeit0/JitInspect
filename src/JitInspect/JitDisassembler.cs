using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Diagnostics.Runtime;

namespace JitInspect;

public sealed class JitDisassembler(ClrRuntime runtime) : IDisposable
{
    internal ConcurrentBag<IDisposable> Disposables { get; } = new ConcurrentBag<IDisposable>();

    public static JitDisassembler Create()
    {
        var dt = CreateDataTarget();
        var info = dt.ClrVersions[0];
        var runtime = info.CreateRuntime();
        var decompiler = new JitDisassembler(runtime);
        decompiler.Disposables.Add(dt);
        decompiler.Disposables.Add(runtime);
        return decompiler;
    }

    public static JitDisassembler Create(DataTarget dt)
    {
        var info = dt.ClrVersions[0];
        var runtime = info.CreateRuntime();
        var decompiler = new JitDisassembler(runtime);
        decompiler.Disposables.Add(runtime);
        return decompiler;
    }

    public static DataTarget CreateDataTarget()
    {
        return DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, false);
    }

    static readonly FormatterOptions formatterOptions = new()
    {
        HexPrefix = "0x",
        HexSuffix = null,
        UppercaseHex = false,
        SpaceAfterOperandSeparator = true
    };

    public string Disassemble(MethodBase method)
    {
        using var writer = new ArrayPoolBufferWriter<char>();
        Disassemble(writer, method);
        return writer.AsSpan().ToString();
    }

    public string Disassemble(Delegate @delegate)
    {
        return Disassemble(@delegate.Method);
    }


    public void Disassemble(IBufferWriter<char> writer, MethodBase method)
    {
        if (method.IsGenericMethodDefinition)
        {
            WriteIgnoredOpenGeneric(writer, method);
        }
        else
        {
            DisassembleConstructedMethod(writer, method);
        }
    }

    void DisassembleConstructedMethod(IBufferWriter<char> writer, MethodBase method)
    {
        RuntimeMethodHandle handle = method.MethodHandle;
        handle.GetFunctionPointer();
        RuntimeHelpers.PrepareMethod(handle);

        ClrMethodData? clrMethodData;
        if (method.IsVirtual && !method.DeclaringType!.IsClass)
        {
            lock (runtime)
                runtime.FlushCachedData();
            var cl = runtime.GetMethodByInstructionPointer((ulong)FunctionPointerHelper.GetMethodPointer((MethodInfo)method));

            clrMethodData = cl != null ? new ClrMethodData(cl.NativeCode, cl.HotColdInfo.HotSize) : null;
        }
        else
        {
            clrMethodData = FindJitCompiledMethod(handle);
        }

        WriteSignatureFromReflection(writer, method);
        if (clrMethodData == null)
        {
            writer.WriteLine("    ; Failed to find JIT output.");
            return;
        }

        var methodAddress = clrMethodData.Value.MethodAddress;
        var methodLength = clrMethodData.Value.MethodSize;


        var reader = new MemoryCodeReader(new IntPtr(unchecked((long)methodAddress)), methodLength);
        var decoder = Decoder.Create(GetBitness(runtime.DataTarget.DataReader.Architecture), reader);
        var instructions = new InstructionList();
        decoder.IP = methodAddress;
        while (decoder.IP < (methodAddress + methodLength))
        {
            decoder.Decode(out instructions.AllocUninitializedElement());
        }

        var resolver = new AsmSymbolResolver(runtime, methodAddress, methodLength);
        var formatter = new IntelFormatter(formatterOptions, resolver);
        var output = new DirectFormatterOutput(writer);
        foreach (ref var instruction in instructions)
        {
            writer.Write("    L");
            writer.Write((instruction.IP - methodAddress).ToString("x4"));
            writer.Write(": ");
            formatter.Format(instruction, output);
            writer.WriteLine();
        }
    }


    ClrMethodData? FindJitCompiledMethod(RuntimeMethodHandle handle)
    {
        handle.GetFunctionPointer();
        RuntimeHelpers.PrepareMethod(handle);
        lock (runtime)
            runtime.FlushCachedData();

        var methodDescAddress = unchecked((ulong)handle.Value.ToInt64());
        if (runtime.GetMethodByHandle(methodDescAddress) is not { } method)
        {
            return null;
        }

        if (method.CompilationType == MethodCompilationType.None || method.NativeCode == 0 || method.HotColdInfo.HotSize == 0)
        {
            return null;
        }

        return new(
            method.NativeCode,
            method.HotColdInfo.HotSize
        );
    }

    int GetBitness(Architecture architecture) => architecture switch
    {
        Architecture.X64 => 64,
        Architecture.X86 => 32,
        _ => throw new Exception($"Unsupported architecture {architecture}.")
    };

    private void WriteIgnoredOpenGeneric(IBufferWriter<char> writer, MethodBase method)
    {
        WriteSignatureFromReflection(writer, method);
        writer.WriteLine("    ; Open generics cannot be JIT-compiled.");
    }

    void WriteTypeName(IBufferWriter<char> writer, Type type)
    {
        if (type.IsByRef)
        {
            WriteTypeName(writer, type.GetElementType()!);
            writer.Write(" ByRef");
            return;
        }

        writer.Write(type.FullName ?? type.Name);
    }

    void WriteSignatureFromReflection(IBufferWriter<char> writer, MethodBase method)
    {
        writer.WriteLine();
        if (method.DeclaringType is { } declaringType)
        {
            writer.Write(declaringType.FullName);
            writer.Write(".");
        }

        writer.Write(method.Name);
        if (method.IsGenericMethod)
        {
            writer.Write("<");
            var first = true;
            foreach (var type in method.GetGenericArguments())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    writer.Write(", ");
                }

                WriteTypeName(writer, type);
            }

            writer.Write(">");
        }

        writer.Write("(");
        {
            var first = true;
            foreach (var parameter in method.GetParameters())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    writer.Write(", ");
                }

                var type = parameter.ParameterType;
                WriteTypeName(writer, type);
            }
        }
        writer.WriteLine(")");
    }

    public void Dispose()
    {
        foreach (var disposable in Disposables)
            disposable.Dispose();
        Disposables.Clear();
    }
}