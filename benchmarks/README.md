# Benchmarks and the performance gate

The benchmarks measure what limits a tunnel's speed: encrypting and
decrypting packets, the per-tick `ManageKeys` call, and creating a context.
CI runs them on every push and pull request, and **a release cannot be
published if performance regressed**, unless the change is accepted by
committing a new baseline.

## Commands

Always run in Release, from anywhere in the repository:

```sh
# Run the benchmarks and compare them with the baseline (what CI does).
# Exits with 1 and prints which benchmark regressed.
dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- check

# Accept the current numbers: overwrite benchmarks/baseline/<os>-<arch>.json.
dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- baseline

# Explore with plain BenchmarkDotNet, e.g. only the encryption benchmarks:
dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- --filter *Encrypt*
```

A full run takes about 1.5 minutes.

## What is checked

The gate applies three rules. The thresholds are in `Gate/GateComparison.cs`.

| Rule | Limit | Why it is reliable |
| --- | --- | --- |
| Allocations per operation | must not grow at all | Allocations are deterministic. The packet path allocates nothing today. |
| Wrapper overhead | the wrapper/direct time ratio may grow by at most 15 % | Each wrapper benchmark has a `Direct` twin that calls libkyrka with nothing around it (`DirectPath.cs`). Both run the same native code on the same machine, so the ratio barely depends on the hardware. |
| Time | at most 35 % slower | Times are divided by the `Calibration` benchmark (plain CPU work) to cancel out machine speed. That also catches slowdowns in the native library, such as a libkyrka or libsodium update or changed build flags. Machines differ in more than speed, so the limit is wide. |

On the machine the baselines were recorded on, repeated runs varied by up to
±8 % in time and ±7 % in overhead ratio, well within the limits. A deliberate
slowdown of a few hundred nanoseconds per packet, and a single 24-byte
allocation, were both caught.

The packet path allocating nothing is also covered by
`tests/LibKyrka.Tests/AllocationTests.cs`, which runs with the normal tests.

## When the gate fails

1. Open the CI run's summary page. It shows the comparison table and which
   rule failed.
2. If the slowdown is a mistake, fix it.
3. If it is intended (for example, a security fix that costs time), accept
   it by updating the baseline in the same pull request. That makes the
   performance change visible in review. For CI, use the `linux-x64.json`
   from the run's `benchmark-results` artifact: it was measured on CI's own
   hardware. Copy it to `benchmarks/baseline/`.

If a benchmark was much faster, the gate passes and suggests updating the
baseline. That keeps the limits tight, so later slowdowns are noticed.

## Baselines

| File | Recorded on | Used by |
| --- | --- | --- |
| `baseline/linux-x64.json` | Linux (Docker), .NET 11 | CI |
| `baseline/win-x64.json` | Windows 11, .NET 11 | local runs on Windows |

Both were recorded on the same development machine. Thanks to the
calibration, the CI baseline does not have to come from CI hardware. If the
first CI runs show that machine differences still trip the time rule,
replace `linux-x64.json` with the one from the `benchmark-results` artifact.

Never change `CalibrationBenchmark`: that would invalidate every baseline.
