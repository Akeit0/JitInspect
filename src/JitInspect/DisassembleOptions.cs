namespace JitInspect;

public class DisassembleOptions
{
    public bool WriteMethodSignature { get; set; } = true;
    public int Indentation { get; set; } = 4;
    public bool WriteILToNativeMap { get; set; } = false;
}