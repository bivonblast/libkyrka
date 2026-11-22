using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibKyrka.Interop;

namespace LibKyrka.Benchmarks;

/// <summary>
/// The least code that can move a packet through libkyrka: the same native
/// calls <see cref="KyrkaContext"/> makes, minus everything the wrapper adds
/// (the lock, disposal checks, wiping the packet buffer and handler exception
/// handling). It is a reference point for measuring the wrapper's overhead,
/// not an example to copy: it skips the safety the wrapper exists to provide.
/// </summary>
/// <remarks>
/// It takes over the native callbacks of two established contexts, so they
/// must not be used through their public API afterwards.
/// </remarks>
internal sealed unsafe class DirectPath : IDisposable
{
    private readonly KyrkaSafeHandle _alice;
    private readonly KyrkaSafeHandle _bob;
    private readonly byte[] _wire = new byte[Kyrka.PacketBufferLength];
    private int _wireLength;
    private GCHandle _self;

    public DirectPath(KyrkaContext alice, KyrkaContext bob)
    {
        _alice = alice.Handle;
        _bob = bob.Handle;
        _self = GCHandle.Alloc(this);

        var self = GCHandle.ToIntPtr(_self);
        Check(NativeMethods.kyrka_purgatory_ifc(_alice, &OnCiphertext, self));
        Check(NativeMethods.kyrka_heaven_ifc(_bob, &OnPlaintext, self));
    }

    /// <summary>Encrypts on Alice; the ciphertext lands in the wire buffer.</summary>
    [SkipLocalsInit]
    public void Encrypt(ReadOnlySpan<byte> plaintext)
    {
        KyrkaPacketNative packet;
        nuint capacity;
        var buffer = NativeMethods.kyrka_packet_databuf(_alice, &packet, &capacity);
        plaintext.CopyTo(new Span<byte>(buffer, plaintext.Length));
        packet.Length = (nuint)plaintext.Length;
        packet.Shroud = (uint)ShroudKey.Peer;
        Check(NativeMethods.kyrka_heaven_input(_alice, &packet));
    }

    /// <summary>Decrypts the wire buffer on Bob.</summary>
    [SkipLocalsInit]
    public void Decrypt()
    {
        KyrkaPacketNative packet;
        nuint capacity;
        var buffer = NativeMethods.kyrka_packet_recvbuf(_bob, &packet, &capacity);
        _wire.AsSpan(0, _wireLength).CopyTo(new Span<byte>(buffer, _wireLength));
        packet.Length = (nuint)_wireLength;
        packet.Shroud = (uint)ShroudKey.Peer;
        Check(NativeMethods.kyrka_purgatory_input(_bob, &packet));
    }

    public void Dispose() => _self.Free();

    private static void Check(int result)
    {
        if (result == -1)
        {
            throw new InvalidOperationException("A libkyrka call failed.");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnCiphertext(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        var path = (DirectPath)GCHandle.FromIntPtr(userData).Target!;
        nuint length;
        var data = NativeMethods.kyrka_packet_sendbuf(path._alice, packet, &length);
        new ReadOnlySpan<byte>(data, (int)length).CopyTo(path._wire);
        path._wireLength = (int)length;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnPlaintext(KyrkaPacketNative* packet, ulong sequence, nint userData)
    {
        // The plaintext is available here; like the wrapper benchmark's
        // handler, do nothing with it.
    }
}
