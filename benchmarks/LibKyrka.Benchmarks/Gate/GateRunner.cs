using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;

namespace LibKyrka.Benchmarks.Gate;

/// <summary>Runs every benchmark with the settings the gate uses and collects the results.</summary>
internal static class GateRunner
{
    public const string CalibrationName = nameof(CalibrationBenchmark.Calibration);

    private static readonly Type[] Benchmarks =
        [typeof(CalibrationBenchmark), typeof(PacketBenchmarks), typeof(ContextBenchmarks)];

    /// <summary>
    /// In-process (no separate build per benchmark, so it is fast and works on
    /// any SDK) with enough iterations for a stable median: about two minutes.
    /// </summary>
    public static IConfig Config(string artifactsPath) =>
        ManualConfig.CreateMinimumViable()
            .AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithLaunchCount(1)
                .WithWarmupCount(5)
                .WithIterationCount(20)
                .WithIterationTime(TimeInterval.FromMilliseconds(200))
                .WithId("Gate"))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .WithArtifactsPath(artifactsPath)
            .WithOptions(ConfigOptions.JoinSummary);

    public static SortedDictionary<string, Measurement> Run(string artifactsPath)
    {
        var results = new SortedDictionary<string, Measurement>(StringComparer.Ordinal);

        foreach (var summary in BenchmarkRunner.Run(Benchmarks, Config(artifactsPath)))
        {
            foreach (var report in summary.Reports)
            {
                results.Add(NameOf(report), Measure(report));
            }
        }

        if (!results.ContainsKey(CalibrationName))
        {
            throw new InvalidOperationException("The calibration benchmark did not run.");
        }

        return results;
    }

    /// <summary>The method name plus any parameter values, e.g. "Encrypt/64".</summary>
    public static string NameOf(string method, params object[] parameters) =>
        parameters.Length == 0 ? method : $"{method}/{string.Join("/", parameters)}";

    private static string NameOf(BenchmarkReport report)
    {
        var benchmark = report.BenchmarkCase;
        var parameters = benchmark.Parameters.Items.Select(p => p.Value).ToArray();
        return NameOf(benchmark.Descriptor.WorkloadMethod.Name, parameters);
    }

    private static Measurement Measure(BenchmarkReport report)
    {
        var statistics = report.ResultStatistics
            ?? throw new InvalidOperationException($"{NameOf(report)} failed; see the log above.");
        var allocated = report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0;
        return new Measurement(statistics.Median, allocated);
    }
}
