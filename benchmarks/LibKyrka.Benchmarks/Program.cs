using System.Runtime.InteropServices;
using BenchmarkDotNet.Running;
using LibKyrka.Benchmarks.Gate;

namespace LibKyrka.Benchmarks;

/// <summary>
/// dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- &lt;command&gt;
/// <list type="bullet">
/// <item><c>check</c>: run the benchmarks and compare them with the baseline;
/// exits with 1 if performance regressed. This is what CI runs.</item>
/// <item><c>baseline</c>: run the benchmarks and save them as the new baseline.</item>
/// <item>anything else: passed to BenchmarkDotNet, e.g. <c>--filter *Encrypt*</c>
/// to explore; without arguments it asks which benchmarks to run.</item>
/// </list>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var repository = FindRepositoryRoot();
        var platform = $"{OperatingSystemName()}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
        var baselinePath = Path.Combine(repository, "benchmarks", "baseline", $"{platform}.json");
        var artifacts = Path.Combine(repository, "artifacts", "benchmarks");

        switch (args.FirstOrDefault())
        {
            case "check":
                return Check(baselinePath, artifacts, platform);

            case "baseline":
                Baseline.FromCurrentMachine(GateRunner.Run(artifacts)).Save(baselinePath);
                Console.WriteLine($"Saved {Path.GetRelativePath(repository, baselinePath)}; commit it to accept these numbers.");
                return 0;

            default:
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, GateRunner.Config(artifacts));
                return 0;
        }
    }

    private static int Check(string baselinePath, string artifacts, string platform)
    {
        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"There is no baseline for {platform}. Create one with the 'baseline' command.");
            return 2;
        }

        var current = Baseline.FromCurrentMachine(GateRunner.Run(artifacts));

        // Saved so CI can offer it as a ready-to-commit baseline from its own hardware.
        current.Save(Path.Combine(artifacts, $"{platform}.json"));

        var result = GateComparison.Compare(Baseline.Load(baselinePath), current);
        var report = result.ToMarkdown($"benchmarks/baseline/{platform}.json");
        Console.WriteLine();
        Console.WriteLine(report);

        if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
        {
            File.AppendAllText(summary, report);
        }

        return result.Passed ? 0 : 1;
    }

    private static string OperatingSystemName() =>
        OperatingSystem.IsWindows() ? "win" :
        OperatingSystem.IsMacOS() ? "osx" :
        OperatingSystem.IsLinux() ? "linux" :
        throw new PlatformNotSupportedException();

    /// <summary>The folder with LibKyrka.slnx, so the paths work from any working directory.</summary>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibKyrka.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Run the benchmarks from within the repository.");
    }
}
