namespace JitInspect;

public class DisassembleOptions
{
    public bool WriteMethodSignature { get; set; } = true;
    public bool WriteILToNativeMap { get; set; } = false;
    
    public DisassemblySyntax Syntax { get; set; } = DisassemblySyntax.Masm;
    public int MaxRecursiveDepth { get; set; } = 0;
}