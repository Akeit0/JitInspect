using System.Buffers;
using Iced.Intel;

namespace JitInspect;

internal class DirectFormatterOutput(IBufferWriter<char> writer) : FormatterOutput
{
    public override void Write(string text, FormatterTextKind kind)
    {
        writer.Write(text);
    }
}