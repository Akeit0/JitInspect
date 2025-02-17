using System.Reflection;

namespace JitInspect;

/// <summary>
/// Provides extension methods for disassembling methods.
/// </summary>
public static class DisassembleExtensions
{
    /// <summary>
    /// Disassembles the given method.
    /// </summary>
    public static string Disassemble(this MethodBase methodBase)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase);
    }
}