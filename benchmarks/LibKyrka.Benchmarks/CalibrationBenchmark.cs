using BenchmarkDotNet.Attributes;

namespace LibKyrka.Benchmarks;

/// <summary>
/// A fixed amount of plain CPU work, unrelated to libkyrka, that measures how
/// fast the machine is. The performance gate divides every result by this
/// one, so a baseline recorded on one machine can be compared with results
/// from a faster or slower one (such as a different CI runner).
/// </summary>
/// <remarks>
/// FNV-1a is a chain of dependent multiplications, so the compiler cannot
/// vectorize it and it measures raw single-core speed. Never change it:
/// that would invalidate every baseline.
/// </remarks>
public class CalibrationBenchmark
{
    private readonly byte[] _data = CreateData();

    [Benchmark]
    public uint Calibration()
    {
        var hash = 2166136261u;
        foreach (var value in _data)
        {
            hash = (hash ^ value) * 16777619u;
        }

        return hash;
    }

    private static byte[] CreateData()
    {
        var data = new byte[4096];
        new Random(42).NextBytes(data);
        return data;
    }
}
