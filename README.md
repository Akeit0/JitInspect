# JitInspect

[![NuGet](https://img.shields.io/nuget/v/JitInspect.svg)](https://www.nuget.org/packages/JitInspect)
[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)

JitInspect disassembles JIT compiled managed methods to a x86/x64/Arm64 ASM.
Inspired by [JitBuddy](https://github.com/xoofx/JitBuddy)

## NuGet

https://www.nuget.org/packages/JitInspect

```
dotnet add package JitInspect
```

## Usage

```C#
using JitInspect;
TestMethod(1, 1);
var action = TestMethod;
Console.WriteLine(action.Method.Disassemble(maxRecursiveDepth: 1, printSource: true));

static void TestMethod(int a, int b)
{
    for (int i = 0; i < a; i++)
        Console.WriteLine(a + b);
}
````
Output:
2
```asm
; Program.<<Main>$>g__TestMethod|0_0(Int32, Int32)
       push      rbp
       sub       rsp,70
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-3C],eax
       mov       [rbp+10],ecx
       mov       [rbp+18],edx
;     for (int i = 0; i < a; i++)
;          ^^^^^^^^^
       xor       eax,eax
       mov       [rbp-3C],eax
       mov       dword ptr [rbp-48],3E8
       jmp       short M00_L01
M00_L00:
       mov       rcx,7FFF7F562790
       call      CORINFO_HELP_COUNTPROFILE32
;         Console.WriteLine(a + b);
;         ^^^^^^^^^^^^^^^^^^^^^^^^^
       mov       eax,[rbp+10]
       mov       ecx,eax
       add       ecx,[rbp+18]
       call      qword ptr [7FFF7F5446C0]; System.Console.WriteLine(Int32)
       mov       eax,[rbp-3C]
       inc       eax
       mov       [rbp-3C],eax
M00_L01:
       mov       eax,[rbp-48]
       dec       eax
       mov       [rbp-48],eax
       cmp       dword ptr [rbp-48],0
       jg        short M00_L02
       lea       rcx,[rbp-48]
       mov       edx,10
       call      CORINFO_HELP_PATCHPOINT
M00_L02:
       mov       eax,[rbp-3C]
       cmp       eax,[rbp+10]
       jl        short M00_L00
       mov       rcx,7FFF7F562794
       call      CORINFO_HELP_COUNTPROFILE32
       nop
       add       rsp,70
       pop       rbp
       ret
; Total bytes of code 130

; System.Console.WriteLine(Int32)
       push      rbx
       sub       rsp,20
       mov       ebx,ecx
       call      qword ptr [7FF8C9EE4930]
       mov       rcx,rax
       mov       edx,ebx
       lea       r11,[System.IO.ConsoleStream.get_CanSeek()]
       cmp       [rcx],ecx
       call      qword ptr [r11]
       nop
       add       rsp,20
       pop       rbx
       ret
; Total bytes of code 37

```

# Credits

- [JitBuddy](https://github.com/xoofx/JitBuddy)
- [ClrMD](https://github.com/Microsoft/clrmd)
- [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet)
- [iced](https://github.com/icedland/iced)
- [Capstone.NET](Gee.External.Capstone.Arm64)

# LICENSE

MIT