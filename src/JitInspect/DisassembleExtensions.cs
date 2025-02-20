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
    public static string Disassemble(this MethodBase methodBase, DisassembleOptions? options =null)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase, options);
    }
    
    /// <summary>
    /// Disassembles the given method.
    /// </summary>
    public static string Disassemble(this MethodBase methodBase, bool writeMethodSignature, bool writeILToNativeMap = false, DisassemblySyntax syntax = DisassemblySyntax.Masm, int maxRecursiveDepth = 0)
    {
        using var disassembler = JitDisassembler.Create();
        return disassembler.Disassemble(methodBase, new DisassembleOptions
        {
            WriteMethodSignature = writeMethodSignature,
            WriteILToNativeMap = writeILToNativeMap,
            Syntax = syntax,
            MaxRecursiveDepth = maxRecursiveDepth,
        });
    }
}