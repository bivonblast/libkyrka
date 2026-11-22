using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using LibKyrka.Interop;

namespace LibKyrka;

// The entry points libkyrka calls back into. They run on the thread that is
// inside a native call (so Kyrka.NativeLock is already held) and must never
// let an exception escape into native code. A handler exception is stored and
// rethrown by InvokeNative once the native call has returned. While a handler
// runs, _insideHandler is set so calls back into libkyrka are refused.
public sealed unsafe partial class KyrkaContext
{
    // True while this thread runs a user handler (see ThrowIfUnusable).
    [ThreadStatic]
    private static bool _insideHandler;

    private ExceptionDispatchInfo? _handlerException;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnNativeHeavenPacket(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._heavenHandler is not { } handler)
        {
            return;
        }

        _insideHandler = true;
        try
        {
            handler(PacketSpan(NativeMethods.kyrka_packet_data(packet), packet->Length), sequence);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
        finally
        {
            _insideHandler = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnNativePurgatoryPacket(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._purgatoryHandler is not { } handler)
        {
            return;
        }

        _insideHandler = true;
        try
        {
            handler(context.GetSendBuffer(packet), sequence);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
        finally
        {
            _insideHandler = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnNativeCathedralPacket(KyrkaPacketNative* packet, ulong magic, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?._cathedralHandler is not { } handler)
        {
            return;
        }

        _insideHandler = true;
        try
        {
            handler(context.GetSendBuffer(packet), (CathedralMessageKind)magic);
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
        finally
        {
            _insideHandler = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnNativeEvent(nint nativeContext, KyrkaEventNative* nativeEvent, nint userData)
    {
        var context = KyrkaSafeHandle.ContextFromUserData(userData);
        if (context?.EventReceived is not { } listeners)
        {
            return;
        }

        _insideHandler = true;
        try
        {
            listeners(KyrkaEventFactory.Create(nativeEvent));
        }
        catch (Exception exception)
        {
            context.CaptureHandlerException(exception);
        }
        finally
        {
            _insideHandler = false;
        }
    }

    /// <summary>
    /// A span over packet bytes in native memory. The length comes from
    /// libkyrka; it is checked against the packet buffer size anyway, so a
    /// wrong value can never expose memory outside the packet.
    /// </summary>
    private static ReadOnlySpan<byte> PacketSpan(byte* data, nuint length)
    {
        if (data == null || length > Kyrka.PacketBufferLength)
        {
            throw new InvalidOperationException($"libkyrka returned an invalid packet ({length} bytes).");
        }

        return new ReadOnlySpan<byte>(data, (int)length);
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

        return PacketSpan(buffer, length);
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
