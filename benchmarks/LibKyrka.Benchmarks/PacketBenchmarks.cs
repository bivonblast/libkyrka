using BenchmarkDotNet.Attributes;

namespace LibKyrka.Benchmarks;

/// <summary>
/// The per-packet path, which is what limits a tunnel's throughput.
/// </summary>
/// <remarks>
/// Each operation is measured twice: through the public API, and through
/// <see cref="DirectPath"/>, which calls libkyrka with nothing around it.
/// The difference is the cost of the wrapper itself, the part this
/// repository controls. 64 bytes is a small packet where that cost shows
/// most; 1400 bytes is a full packet at a typical MTU.
/// </remarks>
[MemoryDiagnoser]
public class PacketBenchmarks
{
    private readonly byte[] _wire = new byte[Kyrka.PacketBufferLength];
    private byte[] _plaintext = [];
    private int _wireLength;
    private EstablishedTunnel _tunnel = null!;
    private DirectPath _direct = null!;

    [Params(64, 1400)]
    public int Size { get; set; }

    [GlobalSetup(Targets = [nameof(Encrypt), nameof(RoundTrip)])]
    public void SetUpWrapper()
    {
        SetUpTunnel();
        _tunnel.Alice.SetPurgatoryHandler(OnCiphertext);
    }

    [GlobalSetup(Targets = [nameof(EncryptDirect), nameof(RoundTripDirect)])]
    public void SetUpDirect()
    {
        SetUpTunnel();
        _direct = new DirectPath(_tunnel.Alice, _tunnel.Bob);
    }

    [GlobalCleanup]
    public void CleanUp()
    {
        _direct?.Dispose();
        _tunnel.Dispose();
    }

    /// <summary>Encrypts a packet (HeavenInput) and copies the ciphertext out.</summary>
    [Benchmark]
    public void Encrypt() => _tunnel.Alice.HeavenInput(_plaintext);

    /// <summary>Encrypts a packet on one peer and decrypts it on the other.</summary>
    [Benchmark]
    public void RoundTrip()
    {
        _tunnel.Alice.HeavenInput(_plaintext);
        _tunnel.Bob.PurgatoryInput(_wire.AsSpan(0, _wireLength));
    }

    [Benchmark]
    public void EncryptDirect() => _direct.Encrypt(_plaintext);

    [Benchmark]
    public void RoundTripDirect()
    {
        _direct.Encrypt(_plaintext);
        _direct.Decrypt();
    }

    private void SetUpTunnel()
    {
        _plaintext = new byte[Size];
        new Random(42).NextBytes(_plaintext);
        _tunnel = EstablishedTunnel.Create();
    }

    private void OnCiphertext(ReadOnlySpan<byte> packet, ulong sequence)
    {
        packet.CopyTo(_wire);
        _wireLength = packet.Length;
    }
}
