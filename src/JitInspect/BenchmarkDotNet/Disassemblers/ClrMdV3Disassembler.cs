using System.Runtime.InteropServices;
using JitInspect;
using Microsoft.Diagnostics.Runtime;

namespace BenchmarkDotNet.Disassemblers;

internal abstract class ClrMdV3Disassembler : JitDisassembler
{
    protected void FlushCachedDataIfNeeded(IDataReader dataTargetDataReader, ulong address, byte[] buffer)
    {
        if (!IsWindows())
        {
            if (dataTargetDataReader.Read(address, buffer) <= 0)
            {
                // We don't suspend the benchmark process for the time of disassembling,
                // as it would require sudo privileges.
                // Because of that, the Tiered JIT thread might still re-compile some methods
                // in the meantime when the host process it trying to disassemble the code.
                // In such case, Tiered JIT thread might create stubs which requires flushing of the cached data.
                dataTargetDataReader.FlushCachedData();
            }
            
        }
    }
#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
#endif
    internal static bool IsWindows() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsWindows(); // prefer linker-friendly OperatingSystem APIs
#else
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
}