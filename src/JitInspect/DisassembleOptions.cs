namespace JitInspect;

public class DisassembleOptions
{
    public DisassemblySyntax Syntax { get; set; } = DisassemblySyntax.Masm;
    public int MaxRecursiveDepth { get; set; } = 0;
    public bool PrintInstructionAddresses { get; set; } = false;
    public bool PrintSource { get; set; } = false;
}