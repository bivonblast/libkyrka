using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;

namespace LibKyrka.Interop;

/// <summary>
/// Translates a native union kyrka_event into a managed <see cref="KyrkaEvent"/>.
/// </summary>
internal static unsafe class KyrkaEventFactory
{
    public static KyrkaEvent Create(KyrkaEventNative* nativeEvent) => nativeEvent->Type switch
    {
        KyrkaEventType.KeysInfo => CreateKeysInfo((KyrkaEventKeysInfoNative*)nativeEvent),
        KyrkaEventType.KeysErased => new KeysErasedEvent(),
        KyrkaEventType.ExchangeInfo => new ExchangeInfoEvent(ReadText((KyrkaEventTextNative*)nativeEvent)),
        KyrkaEventType.PeerDiscovery => CreatePeerDiscovery((KyrkaEventPeerNative*)nativeEvent),
        KyrkaEventType.AmbryReceived => new AmbryReceivedEvent(((KyrkaEventAmbryNative*)nativeEvent)->Generation),
        KyrkaEventType.LiturgyReceived => CreateLiturgy((KyrkaEventLiturgyNative*)nativeEvent),
        KyrkaEventType.RemembranceReceived => CreateRemembrance((KyrkaEventRemembranceNative*)nativeEvent),
        KyrkaEventType.LogMessage => new LogMessageEvent(ReadText((KyrkaEventTextNative*)nativeEvent)),
        var unknown => new UnknownEvent(unknown),
    };

    /// <summary>
    /// Builds an endpoint from an address and port in network byte order, as
    /// sanctum transports them (struct sockaddr_in's sin_addr and sin_port).
    /// </summary>
    internal static IPEndPoint ToEndPoint(uint networkOrderAddress, ushort networkOrderPort)
    {
        var port = BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReverseEndianness(networkOrderPort)
            : networkOrderPort;

        // IPAddress(long) interprets the value in memory order, which is
        // exactly how a network-order address is laid out.
        return new IPEndPoint(new IPAddress(networkOrderAddress), port);
    }

    private static KeysInfoEvent CreateKeysInfo(KyrkaEventKeysInfoNative* keys) =>
        new(keys->TxSpi, keys->RxSpi, keys->PeerId);

    private static PeerDiscoveryEvent CreatePeerDiscovery(KyrkaEventPeerNative* peer) =>
        new(ToEndPoint(peer->Ip, peer->Port), (PeerDiscoveryFlags)peer->Flags);

    private static LiturgyReceivedEvent CreateLiturgy(KyrkaEventLiturgyNative* liturgy) =>
        new(new ReadOnlySpan<byte>(liturgy->Peers, Kyrka.PeersPerFlock).ToArray());

    private static RemembranceReceivedEvent CreateRemembrance(KyrkaEventRemembranceNative* remembrance)
    {
        var cathedrals = new List<IPEndPoint>();

        for (var i = 0; i < Kyrka.MaxCathedrals; i++)
        {
            var address = remembrance->Ips[i];
            var port = (ushort)remembrance->Ports[i];

            if (address != 0 && port != 0)
            {
                cathedrals.Add(ToEndPoint(address, port));
            }
        }

        return new RemembranceReceivedEvent(cathedrals);
    }

    private static string ReadText(KyrkaEventTextNative* text) =>
        Marshal.PtrToStringUTF8((nint)text->Text) ?? string.Empty;
}
