namespace LibKyrka;

/// <summary>
/// Thrown when a libkyrka function reports a failure.
/// </summary>
public sealed class KyrkaException : Exception
{
    /// <summary>Creates an exception for the given libkyrka error.</summary>
    public KyrkaException(KyrkaError error, string operation)
        : base($"{operation} failed: {Describe(error)} ({error}).")
    {
        Error = error;
        Operation = operation;
    }

    /// <summary>The libkyrka error code.</summary>
    public KyrkaError Error { get; }

    /// <summary>The native function that failed, e.g. "kyrka_heaven_input".</summary>
    public string Operation { get; }

    private static string Describe(KyrkaError error) => error switch
    {
        KyrkaError.None => "no error code was set",
        KyrkaError.System => "a system call failed",
        KyrkaError.Parameter => "invalid parameter",
        KyrkaError.NoContext => "no context",
        KyrkaError.NoCallback => "a required packet handler is not set",
        KyrkaError.FileError => "the file cannot be used",
        KyrkaError.Internal => "internal error",
        KyrkaError.Integrity => "integrity check failed",
        KyrkaError.NoTxKey => "no transmit key, the key exchange has not completed",
        KyrkaError.NoRxKey => "no receive key, the key exchange has not completed",
        KyrkaError.NoKek => "no device KEK loaded",
        KyrkaError.NoSecret => "no shared secret loaded",
        KyrkaError.NoConfig => "no cathedral configuration",
        KyrkaError.CathedralConfig => "invalid cathedral configuration",
        KyrkaError.PacketError => "malformed packet",
        KyrkaError.NoCosk => "no cathedral offer signing key loaded",
        KyrkaError.Timeout => "timed out",
        KyrkaError.ShroudNoKeys => "no shroud keys available",
        KyrkaError.MtuSize => "MTU not set or packet too large for it",
        _ => "unknown error",
    };
}
