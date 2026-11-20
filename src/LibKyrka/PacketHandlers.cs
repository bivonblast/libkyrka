namespace LibKyrka;

/// <summary>
/// Receives a packet produced by libkyrka.
/// </summary>
/// <param name="packet">
/// The packet bytes. The span points into native memory and is only valid
/// during the call; copy it if you need to keep it.
/// </param>
/// <param name="sequence">The packet sequence number.</param>
public delegate void PacketHandler(ReadOnlySpan<byte> packet, ulong sequence);

/// <summary>
/// Receives an encrypted packet that must be delivered to a cathedral.
/// </summary>
/// <param name="packet">
/// The packet bytes. The span points into native memory and is only valid
/// during the call; copy it if you need to keep it.
/// </param>
/// <param name="kind">What kind of cathedral message this is.</param>
public delegate void CathedralPacketHandler(ReadOnlySpan<byte> packet, CathedralMessageKind kind);

/// <summary>
/// The kind of message handed to a <see cref="CathedralPacketHandler"/>, which
/// tells the application where to send it. The values are the protocol
/// magics from libkyrka.h.
/// </summary>
public enum CathedralMessageKind : ulong
{
    /// <summary>
    /// A regular cathedral message (KYRKA_CATHEDRAL_MAGIC, "KATEDRAL"). Notifications
    /// and liturgy requests are both sent as this kind, to the cathedral's normal address.
    /// </summary>
    Regular = 0x4b4154454452414c,

    /// <summary>
    /// A NAT detection message (KYRKA_CATHEDRAL_NAT_MAGIC, "CIBORIUM"), sent to the
    /// cathedral's NAT detection address.
    /// </summary>
    NatDetection = 0x4349424f5249554d,
}
