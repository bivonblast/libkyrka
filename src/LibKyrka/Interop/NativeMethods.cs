using System.Runtime.InteropServices;

namespace LibKyrka.Interop;

/// <summary>
/// P/Invoke declarations for the public libkyrka C API (libkyrka.h).
/// Names and signatures mirror the C header one-to-one so the two can be
/// compared side by side when upstream changes.
/// </summary>
/// <remarks>
/// Plain <see cref="DllImportAttribute"/> is used instead of the newer
/// LibraryImport source generator so that one set of declarations works on
/// every target framework, including .NET Standard 2.0. Callbacks are passed
/// as raw function pointers; see <see cref="KyrkaContext"/>'s callback part.
/// </remarks>
internal static unsafe class NativeMethods
{
    /// <summary>Resolves to kyrka.dll, libkyrka.so or libkyrka.dylib.</summary>
    internal const string LibraryName = "kyrka";

    static NativeMethods() => NativeLibraryLoader.Register();

    /// <summary>void (*cb)(KYRKA *, union kyrka_event *, void *)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void EventCallback(nint context, KyrkaEventNative* nativeEvent, nint userData);

    /// <summary>void (*cb)(const void *, size_t, u_int64_t, void *) as used by heaven, purgatory and cathedral.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void PacketCallback(KyrkaPacketNative* packet, ulong value, nint userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte* kyrka_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint kyrka_last_error(KyrkaSafeHandle ctx);

    /// <remarks><c>eventCallback</c> is an <see cref="EventCallback"/> function pointer.</remarks>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint kyrka_ctx_alloc(nint eventCallback, nint userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kyrka_ctx_free(nint ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kyrka_emergency_erase();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_peer_timeout(KyrkaSafeHandle ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_shroud_enable(KyrkaSafeHandle ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_p2p_active(KyrkaSafeHandle ctx, int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_key_manage(KyrkaSafeHandle ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_mtu_size(KyrkaSafeHandle ctx, ushort mtu);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_secret_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_device_kek_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_cosk_load(KyrkaSafeHandle ctx, byte* key, nuint length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_secret_load(KyrkaSafeHandle ctx, byte* secret, nuint length);

    /// <remarks><c>callback</c> is a <see cref="PacketCallback"/> function pointer.</remarks>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_heaven_ifc(KyrkaSafeHandle ctx, nint callback, nint userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_heaven_input(KyrkaSafeHandle ctx, KyrkaPacketNative* packet);

    /// <remarks><c>callback</c> is a <see cref="PacketCallback"/> function pointer.</remarks>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_purgatory_ifc(KyrkaSafeHandle ctx, nint callback, nint userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_purgatory_input(KyrkaSafeHandle ctx, KyrkaPacketNative* packet);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_notify(KyrkaSafeHandle ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_nat_detection(KyrkaSafeHandle ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_liturgy(KyrkaSafeHandle ctx, byte* peers, nuint length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int kyrka_cathedral_config(KyrkaSafeHandle ctx, CathedralConfigNative* config);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte* kyrka_packet_recvbuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte* kyrka_packet_sendbuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte* kyrka_packet_databuf(KyrkaSafeHandle ctx, KyrkaPacketNative* packet, nuint* length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte* kyrka_packet_data(KyrkaPacketNative* packet);
}
