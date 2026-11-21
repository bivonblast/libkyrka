# LibKyrka.Net

.NET bindings for [libkyrka](https://github.com/jorisvink/libkyrka), the
library form of the [sanctum](https://sanctorum.se) protocol: peer-to-peer,
end-to-end encrypted tunnels with a post-quantum (ML-KEM-1024 + X25519) key
exchange. Because it speaks the sanctum protocol, it can talk to sanctum
daemons and use existing sanctum infrastructure such as cathedrals.

The package wraps the real native libkyrka (currently **1.2.2**), so it is
wire-compatible by construction. Native binaries are included for
**win-x64**, **linux-x64**, **linux-arm64** and **osx-arm64**; nothing else
needs to be installed.

```
dotnet add package LibKyrka.Net
```

### Supported frameworks

| Framework | Build used | Notes |
| --- | --- | --- |
| .NET 10, .NET 11 and later | `net10.0` | All four platforms. |
| .NET Framework 4.6.2 – 4.8.1 | `netstandard2.0` | Windows x64; the process must be 64-bit (see below). |
| Other .NET Standard 2.0 runtimes | `netstandard2.0` | For example .NET 8/9 (out of support), Mono, Unity. Expected to work but not tested. |

The Linux binaries need glibc 2.27 or later, the same as .NET 10 itself
(for example RHEL 8, Debian 12, Ubuntu 22.04). musl-based distributions
such as Alpine are not supported yet.

The tests run on .NET 10, .NET 11 and .NET Framework 4.7.2+.

On .NET Framework, the package copies `kyrka.dll` into your output folder
at build time. Only a 64-bit native library is included, so the application
must run as a 64-bit process. SDK-style projects do that by default. In
older project types, set the platform target to x64 or turn off
"Prefer 32-bit".

## How it works

libkyrka does no networking. You move the bytes, which means you can run a
tunnel over UDP, TCP, serial, USB or anything else:

```
 your app ── HeavenInput(plaintext) ──► [libkyrka] ── purgatory handler ──► send to peer
 your app ◄── heaven handler ────────── [libkyrka] ◄── PurgatoryInput(ciphertext) ◄── received
```

- **Heaven** is the cleartext side, **purgatory** the encrypted side.
- `ManageKeys()` must be called on every event loop tick. It runs the key
  exchange, whose packets also come out of the purgatory handler.
- Handlers are invoked synchronously from within the call that caused them.

## Usage

```csharp
using LibKyrka;

using var tunnel = new KyrkaContext();

tunnel.EventReceived += e =>
{
    if (e is KeysInfoEvent { IsEstablished: true })
        Console.WriteLine("tunnel established");
};

// Decrypted data from the peer.
tunnel.SetHeavenHandler((plaintext, sequence) =>
    Console.WriteLine(Encoding.UTF8.GetString(plaintext)));

// Encrypted data to put on the wire. The span is only valid during the call.
tunnel.SetPurgatoryHandler((ciphertext, sequence) =>
    socket.SendTo(ciphertext, peerEndPoint));

tunnel.SetMtu(1422);
tunnel.LoadSecret(sharedSecret);           // 32 bytes, same on both peers
// or: tunnel.LoadSecretFromFile("secret.key");

// Event loop
while (running)
{
    if (socket.Poll(TimeSpan.FromMilliseconds(100), SelectMode.SelectRead))
    {
        var length = socket.Receive(buffer);
        tunnel.PurgatoryInput(buffer.AsSpan(0, length));
    }

    tunnel.ManageKeys();
}

// Once established:
tunnel.HeavenInput("Blessed sanctum, save us"u8);
```

Failures throw `KyrkaException`, whose `Error` property holds the libkyrka
error code (for example `KyrkaError.NoTxKey` before the key exchange has
completed). Packets that fail authentication or are replayed are silently
dropped, as the protocol requires.

### Cathedrals

```csharp
tunnel.ConfigureCathedral(
    new CathedralConfig(flockSource: 0xdeadbeefcafebabe, tunnel: 0x0102, identity: 0xbadf00d),
    (packet, kind) => socket.SendTo(packet, kind == CathedralMessageKind.NatDetection ? natEndPoint : cathedralEndPoint));

tunnel.LoadCathedralSecretFromFile("cathedral.secret");
tunnel.LoadDeviceKekFromFile("device.kek");
tunnel.LoadCathedralSigningKeyFromFile("cathedral.cosk");

tunnel.NotifyCathedral();   // periodically
```

Peer addresses, ambry rollovers and federated cathedrals arrive as
`PeerDiscoveryEvent`, `AmbryReceivedEvent` and `RemembranceReceivedEvent`.

### Threading

libkyrka is not thread-safe, not even across separate contexts. The
bindings serialize every native call on one process-wide lock, so using
contexts from several threads is safe, but the library is designed for a
single-threaded event loop. Do not call into a context from inside its own
handlers; queue the work and do it after the call returns.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/LibKyrka` | The library. `Interop/` mirrors `libkyrka.h` one-to-one; `Polyfills/` fills the gaps in .NET Standard 2.0. |
| `tests/LibKyrka.Tests` | Unit and end-to-end tests against the real native library. |
| `benchmarks/` | Benchmarks and the performance gate that stops regressions from being released ([details](benchmarks/README.md)). |
| `samples/LibKyrka.Demo` | Console demo for local debugging. |
| `native/` | Upstream libkyrka (git submodule) and the scripts that build it. |
| `.github/workflows/ci.yml` | Build, test, pack and publish pipeline. |

## Building from source

Prerequisites: the .NET 11 SDK, and Docker for the native build. The tests
also target .NET 10, which runs on .NET 11 if .NET 10 is not installed, and
.NET Framework 4.7.2 on Windows.

```sh
git clone --recurse-submodules <repository-url>
cd libkyrkaNET

# Build the native libraries (win-x64, linux-x64, linux-arm64) into native/out/.
sh native/build-docker.sh

# On a Mac, also build the macOS library:
sh native/build.sh osx-arm64

dotnet test
```

On Windows, run the `sh` commands from Git Bash. If you cloned without
`--recurse-submodules`, run `git submodule update --init` first.

### Running the demo

```sh
# Two peers in one process, talking over UDP on 127.0.0.1:
dotnet run --project samples/LibKyrka.Demo

# Or two separate processes (type a line and press Enter to send it):
dotnet run --project samples/LibKyrka.Demo -- genkey secret.key
dotnet run --project samples/LibKyrka.Demo -- peer --listen 127.0.0.1:4000 --remote 127.0.0.1:4001 --secret secret.key
dotnet run --project samples/LibKyrka.Demo -- peer --listen 127.0.0.1:4001 --remote 127.0.0.1:4000 --secret secret.key
```

Visual Studio and Rider show the same modes as launch profiles.

### Checking performance

```sh
dotnet run -c Release --project benchmarks/LibKyrka.Benchmarks -- check
```

This compares the benchmarks with the committed baseline, as CI does
before every release. See [benchmarks/README.md](benchmarks/README.md).

### Creating a package locally

```sh
dotnet pack src/LibKyrka -c Release -o artifacts/packages
```

Packing fails unless all four native binaries are present. For a local
test package without the macOS binary, add `-p:KyrkaAllowPartialPackage=true`.

## Releasing

1. Make sure CI on `main` is green.
2. Tag and push: `git tag v1.0.0 && git push origin v1.0.0`.
3. CI builds every platform, runs all tests and the performance gate,
   packs version `1.0.0` and, only if everything passed, pushes it to
   nuget.org.

One-time setup: create an API key on nuget.org, then add it as the secret
`NUGET_API_KEY` in a GitHub environment named `nuget`
(Settings > Environments). Adding required reviewers to that environment
gives you a manual approval step before anything is published.

## Updating libkyrka

1. `cd native/libkyrka && git fetch --tags && git checkout <new tag>`
2. Compare the new `include/libkyrka/libkyrka.h` with `src/LibKyrka/Interop/`
   and update the declarations, structs and constants.
3. If `KYRKA_API_VERSION` changed, update `Kyrka.ApiVersion`. The test
   `NativeApiVersion_MatchesTheVersionTheBindingsWereWrittenFor` fails until
   you do.
4. Rebuild the native libraries, run the tests and the performance check,
   and commit the submodule change. If the new version is slower, the
   performance gate fails; commit an updated baseline to accept it.

## License

ISC, the same license as libkyrka. The package bundles libkyrka, nyfe,
the ML-KEM-1024 reference implementation and libsodium; their licenses
are in THIRD-PARTY-NOTICES.txt, in this repository and in the package.

This is an independent .NET binding and is not affiliated with the
libkyrka or sanctum projects.
