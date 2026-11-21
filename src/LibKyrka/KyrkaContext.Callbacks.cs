using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using LibKyrka.Interop;

namespace LibKyrka;

// The entry points libkyrka calls back into. They run on the thread that is
// inside a native call (so Kyrka.NativeLock is already held) and must never
// let an exception escape into native code. A handler exception is stored and
// rethrown by InvokeNative once the native call has returned.
public sealed unsafe partial class KyrkaContext
{
    // The delegates are kept in static fields so the function pointers handed
    // to libkyrka stay valid for the lifetime of the process. Delegates are
    // used instead of [UnmanagedCallersOnly] because .NET Standard and .NET
    // Framework do not support that attribute.
    private static readonly NativeMethods.EventCallback EventCallback = OnNativeEvent;
    private static readonly NativeMethods.PacketCallback HeavenCallback = OnNativeHeavenPacket;
    private static readonly NativeMethods.PacketCallback PurgatoryCallback = OnNativePurgatoryPacket;
    private static readonly NativeMethods.PacketCallback CathedralCallback = OnNativeCathedralPacket;

    private static readonly nint EventCallbackPointer = Marshal.GetFunctionPointerForDelegate(EventCallback);
    private static readonly nint HeavenCallbackPointer = Marshal.GetFunctionPointerForDelegate(HeavenCallback);
    private static readonly nint PurgatoryCallbackPointer = Marshal.GetFunctionPointerForDelegate(PurgatoryCallback);
    private static readonly nint CathedralCallbackPointer = Marshal.GetFunctionPointerForDelegate(CathedralCallback);

    private ExceptionDispatchInfo? _handlerException;

    private static void OnNativeHeavenPacket(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._heavenHandler is not { } handler)
        {
            return;
        }

        try
        {
            var data = NativeMethods.kyrka_packet_data(packet);
            handler(new ReadOnlySpan<byte>(data, checked((int)packet->Length)), sequence);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
    }

    private static void OnNativePurgatoryPacket(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._purgatoryHandler is not { } handler)
        {
            return;
        }

        try
        {
            handler(context.GetSendBuffer(packet), sequence);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
    }

    private static void OnNativeCathedralPacket(KyrkaPacketNative* packet, ulong magic, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._cathedralHandler is not { } handler)
        {
            return;
        }

        try
        {
            handler(context.GetSendBuffer(packet), (CathedralMessageKind)magic);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
    }

    private static void OnNativeEvent(nint nativeContext, KyrkaEventNative* nativeEvent, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?.EventReceived is not { } listeners)
        {
            return;
        }

        try
        {
            listeners(KyrkaEventFactory.Create(nativeEvent));
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
    }

    /// <summary>The bytes of an outgoing packet as they must go on the wire.</summary>
    private ReadOnlySpan<byte> GetSendBuffer(KyrkaPacketNative* packet)
    {
        nuint length;
        var buffer = NativeMethods.kyrka_packet_sendbuf(_handle, packet, &length);
        if (buffer == null)
        {
            throw LastError("kyrka_packet_sendbuf");
        }

        return new ReadOnlySpan<byte>(buffer, checked((int)length));
    }

    private void CaptureHandlerException(Exception exception) =>
        _handlerException ??= ExceptionDispatchInfo.Capture(exception);

    private void RethrowHandlerException()
    {
        var pending = _handlerException;
        _handlerException = null;
        pending?.Throw();
    }
}
