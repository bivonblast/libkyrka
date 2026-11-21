using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LibKyrka.Interop;

namespace LibKyrka;

/// <summary>
/// Library-wide constants and functions that do not belong to a single
/// <see cref="KyrkaContext"/>.
/// </summary>
public static class Kyrka
{
    /// <summary>
    /// The libkyrka API version these bindings were written against
    /// (KYRKA_API_VERSION). Compare with <see cref="NativeApiVersion"/>.
    /// </summary>
    public const int ApiVersion = 3;

    /// <summary>Length in bytes of shared secrets, cathedral secrets and device KEKs.</summary>
    public const int KeyLength = 32;

    /// <summary>Length in bytes of a cathedral offer signing key (an Ed25519 secret key).</summary>
    public const int CathedralSigningKeyLength = 64;

    /// <summary>The maximum amount of user data carried by one packet (KYRKA_PACKET_DATA_LEN).</summary>
    public const int MaxPacketDataLength = 1500;

    /// <summary>The smallest MTU libkyrka accepts (KYRKA_MTU_MINIMUM).</summary>
    public const int MinimumMtu = 623;

    /// <summary>The maximum MTU libkyrka accepts.</summary>
    public const int MaximumMtu = MaxPacketDataLength;

    /// <summary>The number of peers in a flock (KYRKA_PEERS_PER_FLOCK).</summary>
    public const int PeersPerFlock = 255;

    /// <summary>The maximum number of federated cathedrals (KYRKA_CATHEDRALS_MAX).</summary>
    public const int MaxCathedrals = 32;

    /// <summary>Total size of a packet buffer (KYRKA_PACKET_MAX_LEN).</summary>
    internal const int PacketBufferLength = MaxPacketDataLength + 128;

    /// <summary>
    /// libkyrka is not thread-safe, not even across separate contexts, so
    /// every native call made by these bindings is serialized on this lock.
    /// </summary>
    internal static readonly Lock NativeLock = new();

    /// <summary>
    /// The version string of the loaded native library, including the build
    /// revision, build date and API version, e.g. "1.2.2 (2026-11-20) (api:3)".
    /// </summary>
    public static unsafe string NativeVersion
    {
        get
        {
            lock (NativeLock)
            {
                return Marshal.PtrToStringUTF8((nint)NativeMethods.kyrka_version()) ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// The API version reported by the loaded native library, or -1 if it
    /// cannot be determined.
    /// </summary>
    public static int NativeApiVersion
    {
        get
        {
            var match = Regex.Match(NativeVersion, @"\(api:(\d+)\)", RegexOptions.CultureInvariant);
            return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }
    }

    /// <summary>
    /// Zeroizes the sensitive memory of <b>all</b> contexts in the process.
    /// </summary>
    /// <remarks>
    /// No existing <see cref="KyrkaContext"/> may be used afterwards. Intended
    /// for panic handlers right before the process exits.
    /// </remarks>
    public static void EmergencyErase()
    {
        lock (NativeLock)
        {
            NativeMethods.kyrka_emergency_erase();
        }
    }
}
