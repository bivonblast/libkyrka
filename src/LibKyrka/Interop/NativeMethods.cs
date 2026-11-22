using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

// Never look for the native library in the current directory or on PATH, where
// another program could have placed a malicious kyrka.dll (DLL planting). It is
// loaded from the application's own folders: those listed in its deps.json
// (runtimes/<rid>/native from the NuGet package), the assembly's folder, and
// on Windows also the system folders.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]

namespace LibKyrka.Interop;

/// <summary>
/// P/Invoke declarations for the public libkyrka C API (libkyrka.h).
/// Names and signatures mirror the C header one-to-one so the two can be
/// compared side by side when upstream changes.
/// </summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Resolves to kyrka.dll, libkyrka.so or libkyrka.dylib.</summary>
    private const string LibraryName = "kyrka";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveLibrary);
    }

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* kyrka_version();

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint kyrka_last_error(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint kyrka_ctx_alloc(
        delegate* unmanaged[Cdecl]<nint, KyrkaEventNative*, nint, void> eventCallback,
        nint userData);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void kyrka_ctx_free(nint ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void kyrka_emergency_erase();

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_peer_timeout(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_shroud_enable(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_p2p_active(KyrkaSafeHandle ctx, int active);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_key_manage(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_mtu_size(KyrkaSafeHandle ctx, ushort mtu);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_secret_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_device_kek_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_cosk_load(KyrkaSafeHandle ctx, byte* key, nuint length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_secret_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_heaven_ifc(
        KyrkaSafeHandle ctx,
        delegate* unmanaged[Cdecl]<KyrkaPacketNative*, ulong, nint, void> callback,
        nint userData);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_heaven_input(KyrkaSafeHandle ctx, KyrkaPacketNative* packet);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_purgatory_ifc(
        KyrkaSafeHandle ctx,
        delegate* unmanaged[Cdecl]<KyrkaPacketNative*, ulong, nint, void> callback,
        nint userData);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_purgatory_input(KyrkaSafeHandle ctx, KyrkaPacketNative* packet);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_notify(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_nat_detection(KyrkaSafeHandle ctx);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_liturgy(KyrkaSafeHandle ctx, byte* peers, nuint length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int kyrka_cathedral_config(KyrkaSafeHandle ctx, CathedralConfigNative* config);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* kyrka_packet_recvbuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* kyrka_packet_sendbuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* kyrka_packet_databuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* kyrka_packet_data(KyrkaPacketNative* packet);

    /// <summary>
    /// Finds the native library when the default probing does not.
    /// </summary>
    /// <remarks>
    /// Applications restored from the NuGet package find the library through
    /// their deps.json. Projects that reference LibKyrka directly (the tests
    /// and the demo) only get the files copied to runtimes/&lt;rid&gt;/native/,
    /// so this resolver probes that folder as a fallback.
    /// </remarks>
    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
        {
            return 0;
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
        {
            return handle;
        }

        var candidate = Path.Combine(
            AppContext.BaseDirectory, "runtimes", PortableRuntimeIdentifier(), "native", PlatformFileName());

        return NativeLibrary.TryLoad(candidate, out handle) ? handle : 0;
    }

    private static string PortableRuntimeIdentifier()
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : "linux";

        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return $"{os}-{architecture}";
    }

    private static string PlatformFileName() =>
        OperatingSystem.IsWindows() ? "kyrka.dll"
        : OperatingSystem.IsMacOS() ? "libkyrka.dylib"
        : "libkyrka.so";
}
