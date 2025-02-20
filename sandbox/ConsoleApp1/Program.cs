using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using JitInspect;

var opt = new DisassembleOptions
{
    WriteMethodSignature = true,
    WriteILToNativeMap = true,
    MaxRecursiveDepth = 1,
};

using var stream = File.Create(GetAbsolutePath($"disassembly{Environment.Version.Major}.txt"));
using var decompiler = JitDisassembler.Create();
var t = new TestStruct();
t.Test();
t.Test2();
TestStruct.Add([1],1);
foreach (var m in typeof(TestStruct).GetMethods((BindingFlags)0xffff))
{
    if (m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
    {
        try
        {
            var asm = decompiler.Disassemble(m.MakeGenericMethod(typeof(int)), opt);
            Console.WriteLine(asm);
            stream.WriteLine(asm);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    else
    {
        var asm = decompiler.Disassemble(m, opt);
        Console.WriteLine(asm);
        stream.WriteLine(asm);
    }
}

return;

static string GetAbsolutePath(string relativePath, [CallerFilePath] string callerFilePath = "")
{
    return Path.Combine(Path.GetDirectoryName(callerFilePath)!, relativePath);
}

static class StreamEx
{
    public static void WriteLine(this Stream stream, string buffer)
    {
        var bytes = Encoding.UTF8.GetBytes(buffer);
        stream.Write(bytes);
        stream.WriteByte((byte)'\n');
    }
}

interface ITest
{
    int Test()
    {
        return 0;
    }
}

struct TestDisposable : IDisposable
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
    }
}

class TestClass : ITest
{
    int a = 1;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int Test2()
    {
        return 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int Test()
    {
        if (a == 1)
        {
            a++;
            return Test() + 1;
        }

        return Test2();
    }
}

struct TestStruct() : ITest
{
    int a = 1;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int Test2()
    {
        return 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int Test()
    {
        if (a == 1)
        {
            a++;
            return Test() + 1;
        }

        return Test2() + field[0];
    }

    private static readonly int[] field = Enumerable.Range(0, 100).ToArray();

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Add<T>(List<T> list, T a)
    {
        list.Add(a);
    }
}