using System.Diagnostics;
using System.Security.Cryptography;

namespace LibKyrka.Benchmarks;

/// <summary>
/// Two peers ("Alice" and "Bob") that have completed the key exchange, so
/// data can flow between them. The handlers are left discarding everything;
/// benchmarks install their own.
/// </summary>
internal sealed class EstablishedTunnel : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private EstablishedTunnel(KyrkaContext alice, KyrkaContext bob)
    {
        Alice = alice;
        Bob = bob;
    }

    public KyrkaContext Alice { get; }

    public KyrkaContext Bob { get; }

    public static EstablishedTunnel Create()
    {
        var secret = RandomNumberGenerator.GetBytes(Kyrka.KeyLength);
        var alice = new Peer(secret);
        var bob = new Peer(secret);

        var stopwatch = Stopwatch.StartNew();
        while (!(alice.IsEstablished && bob.IsEstablished))
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new InvalidOperationException("The key exchange did not complete in time.");
            }

            alice.Context.ManageKeys();
            bob.Context.ManageKeys();
            while (alice.Outbox.Count > 0 || bob.Outbox.Count > 0)
            {
                Deliver(alice, bob);
                Deliver(bob, alice);
            }

            Thread.Sleep(10);
        }

        return new EstablishedTunnel(alice.Quiet(), bob.Quiet());
    }

    public void Dispose()
    {
        Alice.Dispose();
        Bob.Dispose();
    }

    private static void Deliver(Peer from, Peer to)
    {
        while (from.Outbox.Count > 0)
        {
            to.Context.PurgatoryInput(from.Outbox.Dequeue());
        }
    }

    private sealed class Peer
    {
        public Peer(byte[] secret)
        {
            Context.EventReceived += e => IsEstablished |= e is KeysInfoEvent { IsEstablished: true };
            Context.SetPurgatoryHandler((packet, _) => Outbox.Enqueue(packet.ToArray()));
            Context.SetMtu(Kyrka.MaximumMtu);
            Context.LoadSecret(secret);
        }

        public KyrkaContext Context { get; } = new();

        public Queue<byte[]> Outbox { get; } = new();

        public bool IsEstablished { get; private set; }

        /// <summary>Stops collecting packets, so nothing allocates while benchmarking.</summary>
        public KyrkaContext Quiet()
        {
            Context.SetPurgatoryHandler(static (_, _) => { });
            Context.SetHeavenHandler(static (_, _) => { });
            return Context;
        }
    }
}
