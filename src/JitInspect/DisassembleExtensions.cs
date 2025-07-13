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
    public static string Disassemble(this MethodBase methodBase, DisassembleOptions options)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase, options);
    }

    /// <summary>
    /// Disassembles the given method.
    /// </summary>
    public static string Disassemble(this MethodBase methodBase, bool printSource = false, bool printInstructionAddresses = false, DisassemblySyntax syntax = DisassemblySyntax.Masm, int maxRecursiveDepth = 0)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase, new()
        {
            PrintSource = printSource,
            PrintInstructionAddresses = printInstructionAddresses,
            Syntax = syntax,
            MaxRecursiveDepth = maxRecursiveDepth
        });
    }
}