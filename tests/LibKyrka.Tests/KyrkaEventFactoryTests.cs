using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using LibKyrka.Interop;
using LibKyrka.Tests.TestSupport;

namespace LibKyrka.Tests;

/// <summary>
/// Translation of native event structs into managed events, without needing
/// a cathedral to produce them.
/// </summary>
public sealed unsafe class KyrkaEventFactoryTests
{
    [Fact]
    public void NativeStructs_HaveTheSameLayoutAsLibkyrka()
    {
        // Sizes of the C structs on 64-bit platforms (all supported RIDs).
        Assert.Equal(1640, sizeof(KyrkaPacketNative));
        Assert.Equal(80, sizeof(CathedralConfigNative));
        Assert.Equal(24, sizeof(KyrkaEventKeysInfoNative));
        Assert.Equal(16, sizeof(KyrkaEventPeerNative));
        Assert.Equal(16, (int)Marshal.OffsetOf<KyrkaEventKeysInfoNative>(nameof(KyrkaEventKeysInfoNative.PeerId)));
        Assert.Equal(72, (int)Marshal.OffsetOf<CathedralConfigNative>(nameof(CathedralConfigNative.Send)));
    }

    [Fact]
    public void ToEndPoint_ConvertsFromNetworkByteOrder()
    {
        var address = TestData.NetworkOrderAddress(192, 168, 1, 20);
        var port = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((ushort)4500) : (ushort)4500;

        var endPoint = KyrkaEventFactory.ToEndPoint(address, port);

        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.1.20"), 4500), endPoint);
    }

    [Fact]
    public void KeysInfo_IsTranslated()
    {
        var native = new KyrkaEventKeysInfoNative { Type = 1, TxSpi = 0x11, RxSpi = 0x22, PeerId = 0x1234 };

        var result = KyrkaEventFactory.Create((KyrkaEventNative*)&native);

        Assert.Equal(new KeysInfoEvent(0x11, 0x22, 0x1234), result);
        Assert.True(((KeysInfoEvent)result).IsEstablished);
    }

    [Fact]
    public void KeysInfo_WithOneDirectionMissing_IsNotEstablished()
    {
        Assert.False(new KeysInfoEvent(0x11, 0, 1).IsEstablished);
        Assert.False(new KeysInfoEvent(0, 0x22, 1).IsEstablished);
    }

    [Fact]
    public void LogMessage_IsTranslated()
    {
        fixed (byte* text = "hello from libkyrka\0"u8)
        {
            var native = new KyrkaEventTextNative { Type = 8, Text = text };

            var result = KyrkaEventFactory.Create((KyrkaEventNative*)&native);

            Assert.Equal(new LogMessageEvent("hello from libkyrka"), result);
        }
    }

    [Fact]
    public void ExchangeInfo_IsTranslated()
    {
        var bytes = Encoding.UTF8.GetBytes("no keys\0");
        fixed (byte* text = bytes)
        {
            var native = new KyrkaEventTextNative { Type = 3, Text = text };

            Assert.Equal(new ExchangeInfoEvent("no keys"), KyrkaEventFactory.Create((KyrkaEventNative*)&native));
        }
    }

    [Fact]
    public void PeerDiscovery_IsTranslated()
    {
        var native = new KyrkaEventPeerNative
        {
            Type = 4,
            Ip = TestData.NetworkOrderAddress(10, 0, 0, 7),
            Port = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((ushort)1234) : (ushort)1234,
            Flags = 1 << 1,
        };

        var result = Assert.IsType<PeerDiscoveryEvent>(KyrkaEventFactory.Create((KyrkaEventNative*)&native));

        Assert.Equal(new IPEndPoint(IPAddress.Parse("10.0.0.7"), 1234), result.Peer);
        Assert.Equal(PeerDiscoveryFlags.SameExternalIPv4, result.Flags);
    }

    [Fact]
    public void AmbryReceived_IsTranslated()
    {
        var native = new KyrkaEventAmbryNative { Type = 5, Generation = 42 };

        Assert.Equal(new AmbryReceivedEvent(42), KyrkaEventFactory.Create((KyrkaEventNative*)&native));
    }

    [Fact]
    public void LiturgyReceived_CopiesAllPeers()
    {
        var native = new KyrkaEventLiturgyNative { Type = 6 };
        native.Peers[0] = 1;
        native.Peers[Kyrka.PeersPerFlock - 1] = 1;

        var result = Assert.IsType<LiturgyReceivedEvent>(KyrkaEventFactory.Create((KyrkaEventNative*)&native));

        Assert.Equal(Kyrka.PeersPerFlock, result.Peers.Count);
        Assert.Equal(1, result.Peers[0]);
        Assert.Equal(0, result.Peers[1]);
        Assert.Equal(1, result.Peers[Kyrka.PeersPerFlock - 1]);
    }

    [Fact]
    public void RemembranceReceived_SkipsEmptySlots()
    {
        var native = new KyrkaEventRemembranceNative { Type = 7 };
        native.Ips[0] = TestData.NetworkOrderAddress(1, 2, 3, 4);
        native.Ports[0] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((ushort)4500) : 4500u;
        native.Ips[5] = TestData.NetworkOrderAddress(5, 6, 7, 8);
        native.Ports[5] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((ushort)4501) : 4501u;

        var result = Assert.IsType<RemembranceReceivedEvent>(KyrkaEventFactory.Create((KyrkaEventNative*)&native));

        Assert.Equal(
            [new IPEndPoint(IPAddress.Parse("1.2.3.4"), 4500), new IPEndPoint(IPAddress.Parse("5.6.7.8"), 4501)],
            result.Cathedrals);
    }

    [Fact]
    public void UnknownType_IsReportedAsUnknownEvent()
    {
        var native = new KyrkaEventNative { Type = 999 };

        Assert.Equal(new UnknownEvent(999), KyrkaEventFactory.Create((KyrkaEventNative*)&native));
    }
}
