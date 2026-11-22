using System.Runtime.InteropServices;

namespace LibKyrka.Interop;

// Managed mirrors of the structs in libkyrka.h. Field order and types must
// match the C definitions exactly; LayoutKind.Sequential applies the same
// natural alignment rules as the C compiler on every supported platform.

/// <summary>struct kyrka_packet.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KyrkaPacketNative
{
    public nuint Length;
    public uint Shroud;
    public fixed byte Data[Kyrka.PacketBufferLength];
}

/// <summary>struct kyrka_cathedral_cfg.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct CathedralConfigNative
{
    public nint UserData;

    public byte* Kek;
    public byte* Cosk;
    public byte* Secret;

    public ulong FlockSource;
    public ulong FlockDestination;

    public byte Hops;
    public ushort Group;
    public ushort Tunnel;
    public uint Identity;

    public int Hidden;
    public int Remembrance;

    public delegate* unmanaged[Cdecl]<KyrkaPacketNative*, ulong, nint, void> Send;
}

/// <summary>
/// The common first member of union kyrka_event. The pointer is cast to one
/// of the specific event structs below once <see cref="Type"/> is known.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KyrkaEventNative
{
    public uint Type;
}

/// <summary>struct kyrka_event_keys_info.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KyrkaEventKeysInfoNative
{
    public uint Type;
    public uint TxSpi;
    public uint RxSpi;
    public ulong PeerId;
}

/// <summary>struct kyrka_event_logmsg and struct kyrka_event_exchange_info.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KyrkaEventTextNative
{
    public uint Type;
    public byte* Text;
}

/// <summary>struct kyrka_event_peer.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KyrkaEventPeerNative
{
    public uint Type;
    public uint Ip;
    public ushort Port;
    public uint Flags;
}

/// <summary>struct kyrka_event_ambry.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KyrkaEventAmbryNative
{
    public uint Type;
    public uint Generation;
}

/// <summary>struct kyrka_event_liturgy.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KyrkaEventLiturgyNative
{
    public uint Type;
    public fixed byte Peers[Kyrka.PeersPerFlock];
}

/// <summary>struct kyrka_event_remembrance.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KyrkaEventRemembranceNative
{
    public uint Type;
    public fixed uint Ips[Kyrka.MaxCathedrals];
    public fixed uint Ports[Kyrka.MaxCathedrals];
}

/// <summary>The KYRKA_EVENT_* constants.</summary>
internal static class KyrkaEventType
{
    public const uint KeysInfo = 1;
    public const uint KeysErased = 2;
    public const uint ExchangeInfo = 3;
    public const uint PeerDiscovery = 4;
    public const uint AmbryReceived = 5;
    public const uint LiturgyReceived = 6;
    public const uint RemembranceReceived = 7;
    public const uint LogMessage = 8;
}
