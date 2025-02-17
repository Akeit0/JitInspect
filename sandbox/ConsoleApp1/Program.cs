using System.Reflection;
using System.Runtime.CompilerServices;
using JitInspect;

var action = TestMethod;
Console.WriteLine(action.Method.Disassemble());


static void TestMethod(int a, int b)
{
    Console.WriteLine(a + b);
}

using var decompiler = JitDisassembler.Create();
foreach (var m in typeof(TestStruct).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
{
    if (m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
    {
        Console.WriteLine(decompiler.Disassemble(m.MakeGenericMethod(typeof(int))));
    }
    else
    {
        Console.WriteLine(decompiler.Disassemble(m));
    }
}
interface ITest
{
    int Test()
    {
        return 0;
    }
}

sealed class TestClass : ITest
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Test()
    {
        return 0x12345678;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int Test2()
    {
        return Test();
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
    
    public int Test()
    {
        if(a == 1)
        {
            a++;
            return Test()+1;
        }
        
        return Test2();
    }
    
    public int Generic<T>(ref T a)
    {
        return 0;
    }
    public int Generic2<T,T1>(ref T a,int[] b)
    {
        return 0;
    }
    
    public int Ref (ref int[] a)
    {
        return 0;
    }
}