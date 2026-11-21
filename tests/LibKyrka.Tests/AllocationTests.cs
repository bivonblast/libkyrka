#if NET
using LibKyrka.Tests.TestSupport;

namespace LibKyrka.Tests;

/// <summary>
/// The packet path runs for every packet a tunnel carries, so it must not
/// allocate: garbage per packet turns into GC pauses under load.
/// </summary>
/// <remarks>
/// Unlike timings, allocations are deterministic, so these checks are exact
/// and run with the normal test suite on every platform, blocking a release
/// like any other test. Timings are checked by the performance gate in
/// benchmarks/. .NET Framework has no per-thread allocation counter, so these
/// tests only run on .NET.
/// </remarks>
public sealed class AllocationTests
{
    private const int Warmup = 200;
    private const int Iterations = 1000;

    [Fact]
    public void PacketRoundTrip_DoesNotAllocate()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();
        var plaintext = TestData.RandomBytes(1400);
        var wire = new Wire();
        var decrypted = new Wire();
        tunnel.Alice.Context.SetPurgatoryHandler(wire.Write);
        tunnel.Bob.Context.SetHeavenHandler(decrypted.Write);

        var allocated = AllocatedBytes(() =>
        {
            tunnel.Alice.Context.HeavenInput(plaintext);
            tunnel.Bob.Context.PurgatoryInput(wire.Packet);
        });

        Assert.Equal(plaintext, decrypted.Packet.ToArray());
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ManageKeys_OnAnEstablishedTunnel_DoesNotAllocate()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();
        var wire = new Wire();
        tunnel.Alice.Context.SetPurgatoryHandler(wire.Write);

        Assert.Equal(0, AllocatedBytes(tunnel.Alice.Context.ManageKeys));
    }

    /// <summary>Bytes allocated on this thread by <paramref name="action"/>, once warmed up.</summary>
    private static long AllocatedBytes(Action action)
    {
        // The first calls JIT-compile code and create the P/Invoke stubs.
        for (var i = 0; i < Warmup; i++)
        {
            action();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>A reusable packet buffer, so the handlers themselves allocate nothing.</summary>
    private sealed class Wire
    {
        private readonly byte[] _buffer = new byte[Kyrka.PacketBufferLength];
        private int _length;

        public ReadOnlySpan<byte> Packet => _buffer.AsSpan(0, _length);

        public void Write(ReadOnlySpan<byte> packet, ulong sequence)
        {
            packet.CopyTo(_buffer);
            _length = packet.Length;
        }
    }
}
#endif
