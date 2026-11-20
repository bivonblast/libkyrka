namespace LibKyrka;

/// <summary>
/// Settings for using a cathedral for peer discovery, relaying and key
/// distribution. Mirrors struct kyrka_cathedral_cfg.
/// </summary>
/// <remarks>
/// The keys (cathedral secret, device KEK and optional signing key) are
/// loaded separately through <see cref="KyrkaContext.LoadCathedralSecret(ReadOnlySpan{byte})"/>,
/// <see cref="KyrkaContext.LoadDeviceKek(ReadOnlySpan{byte})"/> and
/// <see cref="KyrkaContext.LoadCathedralSigningKey(ReadOnlySpan{byte})"/>.
/// </remarks>
public sealed record CathedralConfig
{
    /// <summary>Our flock. Must not be zero.</summary>
    public required ulong FlockSource { get; init; }

    /// <summary>The peer's flock, or zero when it is the same as <see cref="FlockSource"/>.</summary>
    public ulong FlockDestination { get; init; }

    /// <summary>The tunnel we represent. Must not be zero.</summary>
    public required ushort Tunnel { get; init; }

    /// <summary>Our cathedral identity. Must not be zero.</summary>
    public required uint Identity { get; init; }

    /// <summary>
    /// Number of cathedral hops for commixtion. Zero disables it; any other
    /// value requires shroud to be enabled first.
    /// </summary>
    public byte Hops { get; init; }

    /// <summary>The cathedral group.</summary>
    public ushort Group { get; init; }

    /// <summary>Whether this peer is hidden from the cathedral's peer discovery.</summary>
    public bool Hidden { get; init; }

    /// <summary>Whether to request the list of federated cathedrals (remembrance).</summary>
    public bool Remembrance { get; init; }
}
