using System.Diagnostics;
using System.Security.Cryptography;

namespace LibKyrka.Tests.TestSupport;

/// <summary>
/// Two <see cref="KyrkaContext"/> peers ("Alice" and "Bob") connected by an
/// in-memory wire. Packets written to the wire are queued and only delivered
/// by <see cref="Pump"/>, the same way an event loop would do it.
/// </summary>
internal sealed class LoopbackTunnel : IDisposable
{
    public LoopbackTunnel()
        : this(RandomNumberGenerator.GetBytes(Kyrka.KeyLength), RandomNumberGenerator.GetBytes(Kyrka.KeyLength))
    {
    }

    private LoopbackTunnel(byte[] aliceSecret, byte[] bobSecret)
    {
        Alice = new Peer("alice", aliceSecret);
        Bob = new Peer("bob", bobSecret);
    }

    public Peer Alice { get; }

    public Peer Bob { get; }

    /// <summary>Optional hook that can modify (or drop, by returning null) packets on the wire.</summary>
    public Func<byte[], byte[]?>? WireInterceptor { get; set; }

    public static LoopbackTunnel WithSharedSecret()
    {
        var secret = RandomNumberGenerator.GetBytes(Kyrka.KeyLength);
        return new LoopbackTunnel(secret, secret);
    }

    public static LoopbackTunnel WithDifferentSecrets() => new();

    /// <summary>Runs one event loop tick: manage keys on both peers, then deliver queued packets.</summary>
    public void Pump()
    {
        Alice.Context.ManageKeys();
        Bob.Context.ManageKeys();
        Deliver();
    }

    /// <summary>Delivers everything currently queued on the wire, in both directions.</summary>
    public void Deliver()
    {
        // Delivering can queue new packets (responses), so loop until quiet.
        while (Alice.Outbox.Count > 0 || Bob.Outbox.Count > 0)
        {
            DeliverQueued(Alice, Bob);
            DeliverQueued(Bob, Alice);
        }
    }

    /// <summary>Pumps until both peers have keys in both directions, or the timeout expires.</summary>
    public bool TryEstablish(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            Pump();

            if (Alice.IsEstablished && Bob.IsEstablished)
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    public void Establish()
    {
        Assert.True(TryEstablish(TimeSpan.FromSeconds(15)), "The key exchange did not complete in time.");
    }

    public void Dispose()
    {
        Alice.Dispose();
        Bob.Dispose();
    }

    private void DeliverQueued(Peer from, Peer to)
    {
        while (from.Outbox.TryDequeue(out var packet))
        {
            var delivered = WireInterceptor is null ? packet : WireInterceptor(packet);
            if (delivered is not null)
            {
                to.Context.PurgatoryInput(delivered);
            }
        }
    }

    /// <summary>One side of the tunnel and everything it observed.</summary>
    internal sealed class Peer : IDisposable
    {
        public Peer(string name, byte[] secret)
        {
            Name = name;
            Context = new KyrkaContext();
            Context.EventReceived += Events.Add;
            Context.SetPurgatoryHandler((packet, _) => Outbox.Enqueue(packet.ToArray()));
            Context.SetHeavenHandler((packet, sequence) => Received.Add((packet.ToArray(), sequence)));
            Context.SetMtu(Kyrka.MaximumMtu);
            Context.LoadSecret(secret);
        }

        public string Name { get; }

        public KyrkaContext Context { get; }

        /// <summary>Encrypted packets waiting to be delivered to the other peer.</summary>
        public Queue<byte[]> Outbox { get; } = new();

        /// <summary>Decrypted plaintext received from the other peer.</summary>
        public List<(byte[] Data, ulong Sequence)> Received { get; } = [];

        public List<KyrkaEvent> Events { get; } = [];

        public bool IsEstablished => Events.OfType<KeysInfoEvent>().Any(e => e.IsEstablished);

        public void Dispose() => Context.Dispose();

        public override string ToString() => Name;
    }
}
