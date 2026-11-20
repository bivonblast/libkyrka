namespace LibKyrka;

/// <summary>
/// Error codes reported by libkyrka (the KYRKA_ERROR_* constants).
/// </summary>
public enum KyrkaError : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>A system call failed.</summary>
    System = 1,

    /// <summary>An invalid parameter was passed.</summary>
    Parameter = 2,

    /// <summary>No context was given.</summary>
    NoContext = 3,

    /// <summary>A required callback (heaven or purgatory handler) is not set.</summary>
    NoCallback = 4,

    /// <summary>A file could not be used (for example, it is not a regular file).</summary>
    FileError = 5,

    /// <summary>An internal error occurred.</summary>
    Internal = 6,

    /// <summary>An integrity check failed.</summary>
    Integrity = 7,

    /// <summary>There is no transmit key yet; the key exchange has not completed.</summary>
    NoTxKey = 8,

    /// <summary>There is no receive key yet; the key exchange has not completed.</summary>
    NoRxKey = 9,

    /// <summary>No device KEK has been loaded.</summary>
    NoKek = 10,

    /// <summary>No shared secret has been loaded.</summary>
    NoSecret = 11,

    /// <summary>No cathedral configuration has been set.</summary>
    NoConfig = 12,

    /// <summary>The cathedral configuration is invalid.</summary>
    CathedralConfig = 13,

    /// <summary>A packet was malformed.</summary>
    PacketError = 14,

    /// <summary>No cathedral offer signing key has been loaded.</summary>
    NoCosk = 15,

    /// <summary>An operation timed out.</summary>
    Timeout = 16,

    /// <summary>Shroud is enabled but no shroud keys are available yet.</summary>
    ShroudNoKeys = 17,

    /// <summary>The MTU is not set, or the packet does not fit within it.</summary>
    MtuSize = 18,
}
