using System.Globalization;
using System.Text;

namespace LibKyrka.Benchmarks.Gate;

/// <summary>
/// Compares new measurements with the committed baseline and decides whether
/// performance has regressed.
/// </summary>
/// <remarks>
/// <para>Three rules, from most to least precise:</para>
/// <list type="number">
/// <item><b>Allocations</b> may not grow at all. They are deterministic, so
/// this rule is exact.</item>
/// <item><b>Wrapper overhead</b>: each wrapper benchmark is divided by its
/// <c>Direct</c> counterpart (see <see cref="DirectPath"/>). Both run the same
/// native code on the same machine, so the ratio hardly depends on the
/// hardware and catches slowdowns in this repository's C# code even on a
/// different machine than the baseline's.</item>
/// <item><b>Time</b>: every result is divided by the calibration benchmark to
/// cancel out the machine's speed, then compared with the baseline. This also
/// catches slowdowns in the native library (a libkyrka or libsodium update,
/// build flags). Machines differ in more than speed, so this rule needs a
/// wide tolerance.</item>
/// </list>
/// </remarks>
internal static class GateComparison
{
    /// <summary>How much slower (machine-adjusted) a benchmark may get.</summary>
    public const double TimeTolerance = 0.35;

    /// <summary>How much the wrapper/direct ratio may grow.</summary>
    public const double OverheadTolerance = 0.15;

    private const string DirectSuffix = "Direct";

    public static GateResult Compare(Baseline baseline, Baseline current)
    {
        var speed = current.Results[GateRunner.CalibrationName].MedianNanoseconds /
            baseline.Results[GateRunner.CalibrationName].MedianNanoseconds;

        var rows = baseline.Results.Keys
            .Union(current.Results.Keys)
            .Where(name => name != GateRunner.CalibrationName)
            .Order(StringComparer.Ordinal)
            .Select(name => CompareOne(name, baseline, current, speed))
            .ToList();

        return new GateResult(baseline, current, speed, rows);
    }

    private static GateRow CompareOne(string name, Baseline baseline, Baseline current, double speed)
    {
        baseline.Results.TryGetValue(name, out var before);
        current.Results.TryGetValue(name, out var after);

        if (before is null)
        {
            return new GateRow(name, before, after, null, null, Verdict.New);
        }

        if (after is null)
        {
            return new GateRow(name, before, after, null, null, Verdict.Missing);
        }

        var timeChange = after.MedianNanoseconds / speed / before.MedianNanoseconds - 1;
        var overhead = CompareOverhead(name, baseline, current);

        var verdict =
            after.AllocatedBytes > before.AllocatedBytes ? Verdict.AllocatesMore :
            overhead?.Change > OverheadTolerance ? Verdict.WrapperSlower :
            timeChange > TimeTolerance ? Verdict.Slower :
            timeChange < -TimeTolerance ? Verdict.Faster :
            Verdict.Ok;

        return new GateRow(name, before, after, timeChange, overhead, verdict);
    }

    /// <summary>For "Encrypt/64": how Encrypt/64 relates to EncryptDirect/64, before and after.</summary>
    private static Overhead? CompareOverhead(string name, Baseline baseline, Baseline current)
    {
        var separator = name.IndexOf('/');
        var method = separator < 0 ? name : name[..separator];
        var parameters = separator < 0 ? "" : name[separator..];
        var direct = method + DirectSuffix + parameters;

        if (method.EndsWith(DirectSuffix, StringComparison.Ordinal) ||
            !baseline.Results.TryGetValue(direct, out var directBefore) ||
            !current.Results.TryGetValue(direct, out var directAfter))
        {
            return null;
        }

        var before = baseline.Results[name].MedianNanoseconds / directBefore.MedianNanoseconds;
        var after = current.Results[name].MedianNanoseconds / directAfter.MedianNanoseconds;
        return new Overhead(before, after);
    }
}

internal enum Verdict
{
    Ok,
    Faster,
    New,
    Missing,
    AllocatesMore,
    WrapperSlower,
    Slower,
}

/// <summary>The wrapper's time divided by the direct path's time.</summary>
internal sealed record Overhead(double Before, double After)
{
    public double Change => After / Before - 1;
}

internal sealed record GateRow(
    string Name,
    Measurement? Before,
    Measurement? After,
    double? TimeChange,
    Overhead? Overhead,
    Verdict Verdict)
{
    public bool Failed => Verdict is Verdict.Missing or Verdict.AllocatesMore or Verdict.WrapperSlower or Verdict.Slower;
}

internal sealed record GateResult(Baseline Baseline, Baseline Current, double MachineSpeed, IReadOnlyList<GateRow> Rows)
{
    public bool Passed => !Rows.Any(row => row.Failed);

    /// <summary>A GitHub-flavored Markdown report, for the console and the CI job summary.</summary>
    public string ToMarkdown(string baselinePath)
    {
        var text = new StringBuilder();
        text.AppendLine(Passed
            ? "## ✅ Performance gate passed"
            : "## ❌ Performance gate failed: performance regressed");
        text.AppendLine();
        text.AppendLine(Invariant($"Baseline `{baselinePath}`, recorded {Baseline.Recorded:yyyy-MM-dd} on {Baseline.Runtime}."));
        text.AppendLine(Invariant($"This machine is running {Current.Runtime}; its calibration took {MachineSpeed:0.00}x the baseline's time, and times are adjusted by that factor."));
        text.AppendLine();
        text.AppendLine("| Benchmark | Baseline | Now | Change (adjusted) | Wrapper overhead | Allocated | Result |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (var row in Rows)
        {
            text.AppendLine(Invariant(
                $"| {row.Name} | {Time(row.Before)} | {Time(row.After)} | {Change(row.TimeChange)} | {OverheadText(row.Overhead)} | {Allocated(row)} | {Describe(row.Verdict)} |"));
        }

        text.AppendLine();
        text.AppendLine(Invariant(
            $"Limits: {GateComparison.TimeTolerance:P0} slower (machine-adjusted), wrapper overhead ratio {GateComparison.OverheadTolerance:P0} higher, no extra allocations."));

        if (!Passed)
        {
            text.AppendLine();
            text.AppendLine("If the change is intended, accept it by updating the baseline and committing it, so the change is visible in review:");
            text.AppendLine();
            text.AppendLine("```");
            text.AppendLine("dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- baseline");
            text.AppendLine("```");
            text.AppendLine();
            text.AppendLine("In CI, the `benchmark-results` artifact contains a baseline measured on the CI machine.");
        }

        return text.ToString();
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Time(Measurement? measurement) => measurement?.MedianNanoseconds switch
    {
        null => "–",
        >= 1_000_000 and var ns => Invariant($"{ns / 1_000_000:0.00} ms"),
        >= 1_000 and var ns => Invariant($"{ns / 1_000:0.00} µs"),
        var ns => Invariant($"{ns:0} ns"),
    };

    private static string Change(double? change) => change is { } value ? Invariant($"{value:+0%;-0%;0%}") : "–";

    private static string OverheadText(Overhead? overhead) => overhead is null
        ? "–"
        : Invariant($"{overhead.Before - 1:+0%;-0%} → {overhead.After - 1:+0%;-0%}");

    private static string Allocated(GateRow row) => (row.Before, row.After) switch
    {
        ({ } before, { } after) when before.AllocatedBytes != after.AllocatedBytes =>
            Invariant($"{before.AllocatedBytes} B → {after.AllocatedBytes} B"),
        (_, { } after) => Invariant($"{after.AllocatedBytes} B"),
        ({ } before, _) => Invariant($"{before.AllocatedBytes} B"),
        _ => "–",
    };

    private static string Describe(Verdict verdict) => verdict switch
    {
        Verdict.Ok => "✅",
        Verdict.Faster => "⚡ faster; consider updating the baseline",
        Verdict.New => "🆕 not in the baseline",
        Verdict.Missing => "❌ did not run (update the baseline if it was removed)",
        Verdict.AllocatesMore => "❌ allocates more",
        Verdict.WrapperSlower => "❌ wrapper overhead grew",
        Verdict.Slower => "❌ slower",
        _ => verdict.ToString(),
    };
}
