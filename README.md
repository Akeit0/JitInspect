# JitInspect


JitInspect disassembles JIT compiled managed methods to a x86/x64 ASM.
Inspired by [JitBuddy](https://github.com/xoofx/JitBuddy)
```C#
using JitInspect;

var action = TestMethod;
Console.WriteLine(action.Method.Disassemble());


static void TestMethod(int a, int b)
{
    Console.WriteLine(a + b);
}
```

```asm
Program.<<Main>$>g__TestMethod|0_0(System.Int32, System.Int32)
    L0000: nop [rax+rax]
    L0005: add ecx, edx
    L0007: mov rax, 0x7fff2d3109f0
    L0011: jmp rax

```

# LICENSE
MIT
```