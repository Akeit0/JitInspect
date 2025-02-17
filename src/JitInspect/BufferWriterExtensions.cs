using System.Buffers;

namespace JitInspect;

internal static class BufferWriterExtensions
{
    public static readonly string NewLine = Environment.NewLine;

    public static void WriteLine(this IBufferWriter<char> writer, ReadOnlySpan<char> text)
    {
        writer.Write(text);
        writer.Write(NewLine);
    }

    public static void WriteLine(this IBufferWriter<char> writer)
    {
        writer.Write(NewLine);
    }

#if NETSTANDARD2_0
    public static void WriteLine(this IBufferWriter<char> writer, string text)
    {
        writer.Write(text);
        writer.Write(NewLine.);
    }
    public static void Write(this IBufferWriter<char> writer, string text)
    {
        writer.Write(text.AsSpan());
    }
#endif
}