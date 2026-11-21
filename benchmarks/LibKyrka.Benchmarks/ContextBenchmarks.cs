using BenchmarkDotNet.Attributes;

namespace LibKyrka.Benchmarks;

/// <summary>Context lifetime and the per-tick key management call.</summary>
[MemoryDiagnoser]
public class ContextBenchmarks
{
    private EstablishedTunnel _tunnel = null!;

    [GlobalSetup(Target = nameof(ManageKeys))]
    public void SetUp() => _tunnel = EstablishedTunnel.Create();

    [GlobalCleanup(Target = nameof(ManageKeys))]
    public void CleanUp() => _tunnel.Dispose();

    /// <summary>Creates and disposes a context, as done once per tunnel.</summary>
    [Benchmark]
    public void CreateAndDispose()
    {
        using var context = new KyrkaContext();
    }

    /// <summary>One event loop tick on an established tunnel with nothing to do.</summary>
    [Benchmark]
    public void ManageKeys() => _tunnel.Alice.ManageKeys();
}
