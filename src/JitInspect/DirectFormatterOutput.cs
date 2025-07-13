using System.Buffers;
using System.Text;
using Iced.Intel;

namespace JitInspect;

internal class DirectFormatterOutput(IBufferWriter<char> writer) : FormatterOutput
{
    public override void Write(string text, FormatterTextKind kind)
    {
        writer.Write(text);
    }
}

internal class StringBuilderFormatterOutput(StringBuilder writer) : FormatterOutput
{
    public override void Write(string text, FormatterTextKind kind)
    {
        writer.Append(text);
    }
}