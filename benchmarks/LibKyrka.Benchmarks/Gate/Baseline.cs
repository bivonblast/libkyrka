using System.Runtime.InteropServices;
using System.Text.Json;

namespace LibKyrka.Benchmarks.Gate;

/// <summary>One benchmark result: the median time and the bytes allocated per operation.</summary>
internal sealed record Measurement(double MedianNanoseconds, long AllocatedBytes);

/// <summary>
/// A set of measurements and where they were taken. Stored as
/// benchmarks/baseline/&lt;os&gt;-&lt;arch&gt;.json; committing a new one is how a
/// performance change is accepted.
/// </summary>
internal sealed record Baseline(
    string Runtime,
    string OperatingSystem,
    DateTimeOffset Recorded,
    SortedDictionary<string, Measurement> Results)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Baseline FromCurrentMachine(SortedDictionary<string, Measurement> results) =>
        new(RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, DateTimeOffset.UtcNow, results);

    public static Baseline Load(string path) =>
        JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' is empty.");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions) + "\n");
    }
}
