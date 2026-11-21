namespace LibKyrka;

/// <summary>
/// Settings for using a cathedral for peer discovery, relaying and key
/// distribution. Mirrors struct kyrka_cathedral_cfg.
/// </summary>
/// <remarks>
/// <para>
/// The mandatory values are constructor parameters and the optional ones
/// plain properties, rather than <c>required</c>/<c>init</c> members, so the
/// type can be used from every C# version, including the C# 7.3 that .NET
/// Framework projects use by default.
/// </para>
/// <para>
/// The keys (cathedral secret, device KEK and optional signing key) are
/// loaded separately through <see cref="KyrkaContext.LoadCathedralSecret(ReadOnlySpan{byte})"/>,
/// <see cref="KyrkaContext.LoadDeviceKek(ReadOnlySpan{byte})"/> and
/// <see cref="KyrkaContext.LoadCathedralSigningKey(ReadOnlySpan{byte})"/>.
/// </para>
/// </remarks>
public sealed record CathedralConfig
{
    /// <summary>Creates a configuration with the values libkyrka requires.</summary>
    /// <param name="flockSource">Our flock. Must not be zero.</param>
    /// <param name="tunnel">The tunnel we represent. Must not be zero.</param>
    /// <param name="identity">Our cathedral identity. Must not be zero.</param>
    public CathedralConfig(ulong flockSource, ushort tunnel, uint identity)
    {
        FlockSource = flockSource;
        Tunnel = tunnel;
        Identity = identity;
    }

    /// <summary>Our flock. Must not be zero.</summary>
    public ulong FlockSource { get; }

    /// <summary>The tunnel we represent. Must not be zero.</summary>
    public ushort Tunnel { get; }

    /// <summary>Our cathedral identity. Must not be zero.</summary>
    public uint Identity { get; }

    /// <summary>The peer's flock, or zero when it is the same as <see cref="FlockSource"/>.</summary>
    public ulong FlockDestination { get; set; }

    /// <summary>
    /// Number of cathedral hops for commixtion. Zero disables it; any other
    /// value requires shroud to be enabled first.
    /// </summary>
    public byte Hops { get; set; }

    /// <summary>The cathedral group.</summary>
    public ushort Group { get; set; }

    /// <summary>Whether this peer is hidden from the cathedral's peer discovery.</summary>
    public bool Hidden { get; set; }

    /// <summary>Whether to request the list of federated cathedrals (remembrance).</summary>
    public bool Remembrance { get; set; }
}
