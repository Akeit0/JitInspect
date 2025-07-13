using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.RuntimeInformation;

namespace BenchmarkDotNet.Detectors;

public class OsDetector
{
    public static readonly OsDetector Instance = new();


#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
#endif
    internal static bool IsWindows() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsWindows(); // prefer linker-friendly OperatingSystem APIs
#else
        IsOSPlatform(OSPlatform.Windows);
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("linux")]
#endif
    internal static bool IsLinux() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsLinux();
#else
        IsOSPlatform(OSPlatform.Linux);
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("macos")]
#endif
    // ReSharper disable once InconsistentNaming
    internal static bool IsMacOS() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsMacOS();
#else
        IsOSPlatform(OSPlatform.OSX);
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("android")]
#endif
    internal static bool IsAndroid() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsAndroid();
#else
        Type.GetType("Java.Lang.Object, Mono.Android") != null;
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("ios")]
#endif
    // ReSharper disable once InconsistentNaming
    internal static bool IsIOS() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsIOS();
#else
        Type.GetType("Foundation.NSObject, Xamarin.iOS") != null;
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("tvos")]
#endif
    // ReSharper disable once InconsistentNaming
    internal static bool IsTvOS() =>
#if NET6_0_OR_GREATER
        OperatingSystem.IsTvOS();
#else
        IsOSPlatform(OSPlatform.Create("TVOS"));
#endif
}