using System.Reflection;

namespace JitInspect;

public static class DisassembleExtensions
{
    public static string Disassemble(this MethodBase methodBase)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase);
    }
}