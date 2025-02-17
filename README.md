# JitInspect
[![NuGet](https://img.shields.io/nuget/v/JitInspect.svg)](https://www.nuget.org/packages/JitInspect)
[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)

JitInspect disassembles JIT compiled managed methods to a x86/x64 ASM.
Inspired by [JitBuddy](https://github.com/xoofx/JitBuddy)

## NuGet
https://www.nuget.org/packages/JitInspect
```
dotnet add package JitInspect
```

## Usage
```C#
using JitInspect;

var action = TestMethod;
Console.WriteLine(action.Method.Disassemble());


static void TestMethod(int a, int b)
{
    Console.WriteLine(a + b);
}
```
Output:
```asm
Program.<<Main>$>g__TestMethod|0_0(System.Int32, System.Int32)
    L0000: add ecx, edx
    L0002: jmp 0x00007fff1d149690
```



# LICENSE
MIT