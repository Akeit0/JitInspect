using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Symbols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BenchmarkDotNet.Disassemblers;

internal class SourceCodeProvider : IDisposable
{
    readonly Dictionary<SourceFile, string[]> sourceFileCache = new();
    readonly Dictionary<SourceFile, string> sourceFilePathsCache = new();
    readonly Dictionary<PdbInfo, ManagedSymbolModule> pdbReaders = new();
    readonly SymbolReader symbolReader = new(TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath };

    public void Dispose()
    {
        symbolReader.Dispose();
    }

    internal IEnumerable<Sharp> GetSource(ClrMethod method, ILToNativeMap map)
    {
        var sourceLocation = GetSourceLocation(method, map.ILOffset);
        if (sourceLocation == null)
            yield break;

        for (var line = sourceLocation.LineNumber; line <= sourceLocation.LineNumberEnd; ++line)
        {
            var sourceLine = ReadSourceLine(sourceLocation.SourceFile, line);
            if (sourceLine == null)
                continue;

            var text = sourceLine + Environment.NewLine
                                  + GetSmartPointer(sourceLine,
                                      line == sourceLocation.LineNumber ? sourceLocation.ColumnNumber - 1 : default(int?),
                                      line == sourceLocation.LineNumberEnd ? sourceLocation.ColumnNumberEnd - 1 : default(int?));

            yield return new()
            {
                Text = text,
                InstructionPointer = map.StartAddress,
                FilePath = GetFilePath(sourceLocation.SourceFile),
                LineNumber = line
            };
        }
    }

    string GetFilePath(SourceFile sourceFile)
    {
        return sourceFilePathsCache.TryGetValue(sourceFile, out var filePath) ? filePath : sourceFile.Url;
    }

    string ReadSourceLine(SourceFile file, int line)
    {
        if (!sourceFileCache.TryGetValue(file, out var contents))
        {
            // GetSourceFile method returns path when file is stored on the same machine
            // otherwise it downloads it from the Symbol Server and returns the source code ;)
            var wholeFileOrJustPath = file.GetSourceFile();

            if (string.IsNullOrEmpty(wholeFileOrJustPath))
                return null;

            if (File.Exists(wholeFileOrJustPath))
            {
                contents = File.ReadAllLines(wholeFileOrJustPath);
                sourceFilePathsCache.Add(file, wholeFileOrJustPath);
            }
            else
            {
                contents = wholeFileOrJustPath.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            }

            sourceFileCache.Add(file, contents);
        }

        return line - 1 < contents.Length
            ? contents[line - 1]
            : null; // "nop" can have no corresponding c# code ;)
    }

    static string GetSmartPointer(string sourceLine, int? start, int? end)
    {
        Debug.Assert(start is null || start < sourceLine.Length);
        Debug.Assert(end is null || end <= sourceLine.Length);

        var prefix = new char[end ?? sourceLine.Length];
        var index = 0;

        // write offset using whitespaces
        while (index < (start ?? prefix.Length))
        {
            prefix[index] =
                sourceLine.Length > index &&
                sourceLine[index] == '\t'
                    ? '\t'
                    : ' ';
            index++;
        }

        // write smart pointer
        while (index < prefix.Length)
        {
            prefix[index] = '^';
            index++;
        }

        return new(prefix);
    }

    internal SourceLocation GetSourceLocation(ClrMethod method, int ilOffset)
    {
        var reader = GetReaderForMethod(method);
        if (reader == null)
            return null;

        return reader.SourceLocationForManagedCode((uint)method.MetadataToken, ilOffset);
    }

    internal SourceLocation GetSourceLocation(ClrStackFrame frame)
    {
        var reader = GetReaderForMethod(frame.Method);
        if (reader == null)
            return null;

        return reader.SourceLocationForManagedCode((uint)frame.Method.MetadataToken, FindIlOffset(frame));
    }

    static int FindIlOffset(ClrStackFrame frame)
    {
        var ip = frame.InstructionPointer;
        var last = -1;
        foreach (var item in frame.Method.ILOffsetMap)
        {
            if (item.StartAddress > ip)
                return last;

            if (ip <= item.EndAddress)
                return item.ILOffset;

            last = item.ILOffset;
        }

        return last;
    }

    ManagedSymbolModule GetReaderForMethod(ClrMethod method)
    {
        var module = method?.Type?.Module;
        var info = module?.Pdb;

        ManagedSymbolModule? reader = null;
        if (info != null)
            if (!pdbReaders.TryGetValue(info, out reader))
            {
                var pdbPath = info.Path;
                if (pdbPath != null)
                    try
                    {
                        reader = symbolReader.OpenSymbolFile(pdbPath);
                    }
                    catch (IOException)
                    {
                        // This will typically happen when trying to load information
                        // from public symbols, or symbol files generated by some weird
                        // compiler. We can ignore this, but there's no need to load
                        // this PDB anymore, so we will put null in the dictionary and
                        // be done with it.
                        reader = null;
                    }

                pdbReaders[info] = reader;
            }

        return reader;
    }
}