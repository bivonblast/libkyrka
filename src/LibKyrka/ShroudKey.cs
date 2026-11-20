namespace LibKyrka;

/// <summary>
/// Which key to use when shrouding or unshrouding a packet
/// (KYRKA_PACKET_SHROUD_*). Only relevant after
/// <see cref="KyrkaContext.EnableShroud"/> has been called.
/// </summary>
public enum ShroudKey : uint
{
    /// <summary>Use the cathedral shroud key (traffic to or from a cathedral).</summary>
    Cathedral = 1,

    /// <summary>Use the peer shroud key (traffic to or from the peer).</summary>
    Peer = 2,
}
