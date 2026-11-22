using System.Text;
using LibKyrka.Tests.TestSupport;

namespace LibKyrka.Tests;

/// <summary>
/// End-to-end tests: two peers negotiate keys and exchange data through the
/// real native libkyrka, connected by an in-memory wire.
/// </summary>
public sealed class TunnelTests
{
    [Fact]
    public void KeyExchange_WithSharedSecret_Completes()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();

        tunnel.Establish();

        var alice = tunnel.Alice.Events.OfType<KeysInfoEvent>().Last(e => e.IsEstablished);
        var bob = tunnel.Bob.Events.OfType<KeysInfoEvent>().Last(e => e.IsEstablished);

        // What one peer transmits with, the other receives with.
        Assert.Equal(alice.TxSpi, bob.RxSpi);
        Assert.Equal(alice.RxSpi, bob.TxSpi);
    }

    [Fact]
    public void KeyExchange_WithDifferentSecrets_NeverCompletes()
    {
        using var tunnel = LoopbackTunnel.WithDifferentSecrets();

        Assert.False(tunnel.TryEstablish(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Data_FlowsInBothDirections()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        tunnel.Alice.Context.HeavenInput("Blessed sanctum, save us"u8);
        tunnel.Bob.Context.HeavenInput("Hello Alice"u8);
        tunnel.Deliver();

        Assert.Equal("Blessed sanctum, save us", ReceivedText(tunnel.Bob));
        Assert.Equal("Hello Alice", ReceivedText(tunnel.Alice));
    }

    [Fact]
    public void Ciphertext_DoesNotContainThePlaintext()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        var plaintext = Encoding.ASCII.GetBytes("a very recognizable secret message");
        tunnel.Alice.Context.HeavenInput(plaintext);

        var ciphertext = Assert.Single(tunnel.Alice.Outbox);
        Assert.True(ciphertext.Length > plaintext.Length);
        Assert.Equal(-1, ciphertext.AsSpan().IndexOf(plaintext));
    }

    [Fact]
    public void SequenceNumbers_Increase()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        for (var i = 0; i < 5; i++)
        {
            tunnel.Alice.Context.HeavenInput([(byte)i]);
        }

        tunnel.Deliver();

        var sequences = tunnel.Bob.Received.Select(packet => packet.Sequence).ToList();
        Assert.Equal(5, sequences.Count);
        Assert.Equal(sequences.Order(), sequences);
        Assert.Equal(sequences.Distinct(), sequences);
    }

    [Fact]
    public void LargePayload_IsDeliveredIntact()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        var payload = new byte[1400];
        Random.Shared.NextBytes(payload);

        tunnel.Alice.Context.HeavenInput(payload);
        tunnel.Deliver();

        Assert.Equal(payload, Assert.Single(tunnel.Bob.Received).Data);
    }

    [Fact]
    public void Payload_ThatDoesNotFitWithProtocolOverhead_ThrowsMtuSize()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        var exception = Assert.Throws<KyrkaException>(
            () => tunnel.Alice.Context.HeavenInput(new byte[Kyrka.MaxPacketDataLength]));

        Assert.Equal(KyrkaError.MtuSize, exception.Error);
    }

    [Fact]
    public void TamperedPacket_IsDropped()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        tunnel.WireInterceptor = packet =>
        {
            packet[^1] ^= 0x01;
            return packet;
        };

        tunnel.Alice.Context.HeavenInput("tampered"u8);
        tunnel.Deliver();

        Assert.Empty(tunnel.Bob.Received);
    }

    [Fact]
    public void ReplayedPacket_IsDeliveredOnlyOnce()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        tunnel.Alice.Context.HeavenInput("only once"u8);
        var packet = tunnel.Alice.Outbox.Dequeue();

        tunnel.Bob.Context.PurgatoryInput(packet);
        tunnel.Bob.Context.PurgatoryInput(packet);

        Assert.Single(tunnel.Bob.Received);
    }

    [Fact]
    public void PeerTimeout_ErasesKeys_SoSendingFailsUntilRenegotiated()
    {
        using var tunnel = LoopbackTunnel.WithSharedSecret();
        tunnel.Establish();

        tunnel.Alice.Context.NotifyPeerTimeout();

        var exception = Assert.Throws<KyrkaException>(() => tunnel.Alice.Context.HeavenInput("hello"u8));
        Assert.Equal(KyrkaError.NoTxKey, exception.Error);
        Assert.Contains(tunnel.Alice.Events, e => e is KeysErasedEvent);
    }

    private static string ReceivedText(LoopbackTunnel.Peer peer) =>
        Encoding.UTF8.GetString(Assert.Single(peer.Received).Data);
}
