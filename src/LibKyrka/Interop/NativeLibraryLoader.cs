using System.Runtime.InteropServices;
#if NET
using System.Reflection;
#endif

namespace LibKyrka.Interop;

/// <summary>
/// Helps the runtime find the native library when its default probing does not.
/// </summary>
/// <remarks>
/// <para>
/// Applications restored from the NuGet package on modern .NET find the
/// library through their deps.json. Two cases need help: projects that
/// reference LibKyrka directly (the tests and the demo), which only get the
/// files copied to runtimes/&lt;rid&gt;/native/, and .NET Framework, which never
/// looks in runtimes/ at all (the package's build targets copy the library
/// there for it).
/// </para>
/// <para>
/// On .NET 5+ a DllImport resolver probes that folder as a fallback. On
/// .NET Standard there is no resolver API, so on Windows the library is
/// loaded up front instead; DllImport then binds to the already loaded
/// kyrka.dll by name.
/// </para>
/// </remarks>
internal static class NativeLibraryLoader
{
    public static void Register()
    {
#if NET
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, Resolve);
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(BundledLibraryPath))
        {
            // A failure is ignored here: the DllImport will then report the
            // usual DllNotFoundException or BadImageFormatException.
            LoadLibraryW(BundledLibraryPath);
        }
#endif
    }

    /// <summary>runtimes/&lt;rid&gt;/native/&lt;file&gt; below the application folder.</summary>
    private static string BundledLibraryPath =>
        Path.Combine(AppContext.BaseDirectory, "runtimes", PortableRuntimeIdentifier, "native", PlatformFileName);

    private static string PortableRuntimeIdentifier
    {
        get
        {
            var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
                : "linux";

            var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            return $"{os}-{architecture}";
        }
    }

    private static string PlatformFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kyrka.dll"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libkyrka.dylib"
        : "libkyrka.so";

#if NET
    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != NativeMethods.LibraryName)
        {
            return 0;
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
        {
            return handle;
        }

        return NativeLibrary.TryLoad(BundledLibraryPath, out handle) ? handle : 0;
    }
#else
    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint LoadLibraryW(string path);
#endif
}
