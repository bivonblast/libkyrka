using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using LibKyrka;
using LibKyrka.Demo;

// LibKyrka demo: sanctum tunnels over UDP on your own machine.
//
//   loopback [--count N]          Two peers in this process talk to each other.
//   peer --listen ADDR:PORT --remote ADDR:PORT --secret FILE
//                                 One peer; run a second instance (or a real
//                                 sanctum/libkyrka peer) on the other end.
//                                 Type a line and press Enter to send it.
//   genkey FILE                   Write a new random 32-byte shared secret.

try
{
    Console.WriteLine($"libkyrka {Kyrka.NativeVersion}");

    return args switch
    {
        [] or ["loopback", ..] => RunLoopback(args.Skip(1).ToArray()),
        ["peer", .. var rest] => RunPeer(rest),
        ["genkey", var path] => GenerateKey(path),
        _ => Usage(),
    };
}
catch (Exception exception) when (exception is ArgumentException or FormatException or IOException or UnauthorizedAccessException or SocketException or KyrkaException)
{
    Log.Error(exception.Message);
    return 1;
}

static int RunLoopback(string[] options)
{
    var count = int.Parse(Option(options, "--count") ?? "0");
    var secret = RandomNumberGenerator.GetBytes(Kyrka.KeyLength);
    var any = new IPEndPoint(IPAddress.Loopback, 0);

    // Bind both sockets to free ports first, then point them at each other.
    using var alice = new UdpPeer("alice", any, secret);
    using var bob = new UdpPeer("bob", any, secret);
    alice.Remote = bob.LocalEndPoint;
    bob.Remote = alice.LocalEndPoint;

    Console.WriteLine($"alice {alice.LocalEndPoint} <-> bob {bob.LocalEndPoint}" +
        (count > 0 ? $", sending {count} messages" : ", press Ctrl+C to stop"));

    using var stop = StopOnCtrlC();
    var sent = 0;
    var lastSend = Stopwatch.StartNew();
    var deadline = Stopwatch.StartNew();

    while (!stop.IsCancellationRequested)
    {
        alice.Poll(TimeSpan.FromMilliseconds(50));
        bob.Poll(TimeSpan.Zero);

        if (alice.IsEstablished && bob.IsEstablished && lastSend.Elapsed >= TimeSpan.FromSeconds(1))
        {
            lastSend.Restart();
            sent++;
            alice.Send($"Blessed sanctum, save us ({sent})");
            bob.Send($"Hello Alice ({sent})");
        }

        if (count > 0 && sent >= count)
        {
            // One last round so the final messages are received.
            alice.Poll(TimeSpan.FromMilliseconds(100));
            bob.Poll(TimeSpan.FromMilliseconds(100));
            return 0;
        }

        if (count > 0 && deadline.Elapsed > TimeSpan.FromSeconds(30))
        {
            Log.Error("timed out");
            return 1;
        }
    }

    return 0;
}

static int RunPeer(string[] options)
{
    var listen = IPEndPoint.Parse(Option(options, "--listen") ?? throw new ArgumentException("--listen is required"));
    var remote = IPEndPoint.Parse(Option(options, "--remote") ?? throw new ArgumentException("--remote is required"));
    var secretPath = Option(options, "--secret") ?? throw new ArgumentException("--secret is required");

    using var peer = new UdpPeer("peer", listen, secretPath) { Remote = remote };

    Console.WriteLine($"listening on {listen}, peer at {remote}. Type a message and press Enter to send.");

    // Console input is read on a separate thread and handed to the event loop.
    var input = new ConcurrentQueue<string>();
    var reader = new Thread(() =>
    {
        while (Console.ReadLine() is { } line)
        {
            input.Enqueue(line);
        }
    }) { IsBackground = true };
    reader.Start();

    using var stop = StopOnCtrlC();
    while (!stop.IsCancellationRequested)
    {
        peer.Poll(TimeSpan.FromMilliseconds(100));

        while (input.TryDequeue(out var line))
        {
            if (!peer.IsEstablished)
            {
                Log.Warn("peer", "not established yet, message dropped");
            }
            else if (line.Length > 0)
            {
                peer.Send(line);
            }
        }
    }

    return 0;
}

static int GenerateKey(string path)
{
    // Never overwrite an existing key, make the file readable by its owner
    // only (on Linux and macOS), and write without a buffer so no copy of
    // the key is left in memory.
    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, BufferSize = 0 };
    if (!OperatingSystem.IsWindows())
    {
        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    }

    Span<byte> secret = stackalloc byte[Kyrka.KeyLength];
    try
    {
        RandomNumberGenerator.Fill(secret);
        using var file = new FileStream(path, options);
        file.Write(secret);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
    }

    Console.WriteLine($"wrote a new {Kyrka.KeyLength}-byte secret to {Path.GetFullPath(path)}");
    return 0;
}

static int Usage()
{
    Console.WriteLine("""
        usage:
          LibKyrka.Demo loopback [--count N]
          LibKyrka.Demo peer --listen ADDR:PORT --remote ADDR:PORT --secret FILE
          LibKyrka.Demo genkey FILE
        """);
    return 2;
}

static string? Option(string[] options, string name)
{
    var index = Array.IndexOf(options, name);
    return index >= 0 && index + 1 < options.Length ? options[index + 1] : null;
}

static CancellationTokenSource StopOnCtrlC()
{
    var source = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        source.Cancel();
    };
    return source;
}
