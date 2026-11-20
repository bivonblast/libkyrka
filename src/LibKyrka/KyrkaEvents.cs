using System.Net;

namespace LibKyrka;

/// <summary>
/// Base type of every event raised through <see cref="KyrkaContext.EventReceived"/>.
/// Use pattern matching to handle the kinds you care about.
/// </summary>
public abstract record KyrkaEvent;

/// <summary>New keys were installed (KYRKA_EVENT_KEYS_INFO).</summary>
/// <param name="TxSpi">The SPI of the transmit key, or zero if there is none.</param>
/// <param name="RxSpi">The SPI of the receive key, or zero if there is none.</param>
/// <param name="PeerId">The identifier of the peer the keys were negotiated with.</param>
public sealed record KeysInfoEvent(uint TxSpi, uint RxSpi, ulong PeerId) : KyrkaEvent
{
    /// <summary>True when both directions have keys, meaning data can flow.</summary>
    public bool IsEstablished => TxSpi != 0 && RxSpi != 0;
}

/// <summary>All keys were erased, e.g. after a peer timeout (KYRKA_EVENT_KEYS_ERASED).</summary>
public sealed record KeysErasedEvent : KyrkaEvent;

/// <summary>Progress information about the key exchange (KYRKA_EVENT_EXCHANGE_INFO).</summary>
/// <param name="Reason">A human readable description, e.g. "no keys".</param>
public sealed record ExchangeInfoEvent(string Reason) : KyrkaEvent;

/// <summary>A cathedral told us where to reach our peer (KYRKA_EVENT_PEER_DISCOVERY).</summary>
/// <param name="Peer">The peer's public address.</param>
/// <param name="Flags">Additional information from the cathedral.</param>
public sealed record PeerDiscoveryEvent(IPEndPoint Peer, PeerDiscoveryFlags Flags) : KyrkaEvent;

/// <summary>Flags carried by a <see cref="PeerDiscoveryEvent"/>.</summary>
[Flags]
public enum PeerDiscoveryFlags : uint
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>
    /// The cathedral sees the same external IPv4 address for both peers
    /// (KYRKA_INFO_FLAG_SAME_EXTERNAL_IPV4); they are probably on the same network.
    /// </summary>
    SameExternalIPv4 = 1 << 1,
}

/// <summary>A new ambry (shared secret distribution) arrived from the cathedral (KYRKA_EVENT_AMBRY_RECEIVED).</summary>
/// <param name="Generation">The ambry generation.</param>
public sealed record AmbryReceivedEvent(uint Generation) : KyrkaEvent;

/// <summary>A response to a liturgy request arrived (KYRKA_EVENT_LITURGY_RECEIVED).</summary>
/// <param name="Peers">One entry per peer in the flock (<see cref="Kyrka.PeersPerFlock"/> entries).</param>
public sealed record LiturgyReceivedEvent(IReadOnlyList<byte> Peers) : KyrkaEvent;

/// <summary>The list of federated cathedrals arrived (KYRKA_EVENT_REMEMBRANCE_RECEIVED).</summary>
/// <param name="Cathedrals">The cathedral addresses; empty slots are left out.</param>
public sealed record RemembranceReceivedEvent(IReadOnlyList<IPEndPoint> Cathedrals) : KyrkaEvent;

/// <summary>A log message from libkyrka (KYRKA_EVENT_LOGMSG).</summary>
/// <param name="Message">The message text.</param>
public sealed record LogMessageEvent(string Message) : KyrkaEvent;

/// <summary>An event type these bindings do not know about (from a newer libkyrka).</summary>
/// <param name="Type">The raw KYRKA_EVENT_* value.</param>
public sealed record UnknownEvent(uint Type) : KyrkaEvent;
