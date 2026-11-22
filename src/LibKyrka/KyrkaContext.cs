using System.Security.Cryptography;
using LibKyrka.Interop;

namespace LibKyrka;

/// <summary>
/// One sanctum tunnel endpoint: a managed wrapper around a native KYRKA context.
/// </summary>
/// <remarks>
/// <para>
/// libkyrka does no networking itself. Your application moves the bytes:
/// </para>
/// <list type="bullet">
/// <item>Plaintext you want to send goes into <see cref="HeavenInput"/>; the
/// encrypted result comes out of the purgatory handler, ready to be sent.</item>
/// <item>Encrypted data you received goes into <see cref="PurgatoryInput"/>;
/// the decrypted result comes out of the heaven handler.</item>
/// <item><see cref="ManageKeys"/> must be called regularly (every event loop
/// tick, e.g. every 100 ms). It drives the key exchange, which sends its
/// packets through the purgatory handler as well.</item>
/// </list>
/// <para>
/// All handlers and <see cref="EventReceived"/> are invoked synchronously, on
/// the calling thread, from within the method that triggered them. Do not call
/// back into a context from its own handlers; queue the work instead.
/// </para>
/// <para>
/// Calls into libkyrka are serialized on a process-wide lock because the
/// native library is not thread-safe.
/// </para>
/// </remarks>
public sealed unsafe partial class KyrkaContext : IDisposable
{
    private readonly KyrkaSafeHandle _handle;

    // Handlers are referenced here so they live as long as the context.
    private PacketHandler? _heavenHandler;
    private PacketHandler? _purgatoryHandler;
    private CathedralPacketHandler? _cathedralHandler;

    /// <summary>Creates a new context representing a single tunnel.</summary>
    /// <exception cref="KyrkaException">The native context could not be created.</exception>
    public KyrkaContext()
    {
        _handle = new KyrkaSafeHandle();
        _handle.SetOwner(this);

        lock (Kyrka.NativeLock)
        {
            var context = NativeMethods.kyrka_ctx_alloc(&OnNativeEvent, _handle.UserData);
            if (context == 0)
            {
                _handle.Dispose();
                throw new KyrkaException(KyrkaError.Internal, "kyrka_ctx_alloc");
            }

            _handle.SetNativeContext(context);
        }
    }

    /// <summary>
    /// Raised for key exchange progress, log messages and cathedral information.
    /// </summary>
    public event Action<KyrkaEvent>? EventReceived;

    /// <summary>True once <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed => _handle.IsClosed;

    /// <summary>The native context, for benchmarks that bypass the wrapper.</summary>
    internal KyrkaSafeHandle Handle => _handle;

    /// <summary>
    /// Sets the handler that receives decrypted plaintext (kyrka_heaven_ifc).
    /// </summary>
    public void SetHeavenHandler(PacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            _heavenHandler = handler;
            Check(NativeMethods.kyrka_heaven_ifc(_handle, &OnNativeHeavenPacket, _handle.UserData), "kyrka_heaven_ifc");
        }
    }

    /// <summary>
    /// Sets the handler that receives encrypted packets to send to the peer
    /// (kyrka_purgatory_ifc). Required before keys can be negotiated.
    /// </summary>
    public void SetPurgatoryHandler(PacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            _purgatoryHandler = handler;
            Check(NativeMethods.kyrka_purgatory_ifc(_handle, &OnNativePurgatoryPacket, _handle.UserData), "kyrka_purgatory_ifc");
        }
    }

    /// <summary>Loads the 32-byte shared secret both peers use (kyrka_secret_load).</summary>
    public void LoadSecret(ReadOnlySpan<byte> secret) =>
        LoadKey(secret, Kyrka.KeyLength, &NativeMethods.kyrka_secret_load, "kyrka_secret_load");

    /// <summary>Loads the shared secret from a file containing exactly 32 raw bytes.</summary>
    public void LoadSecretFromFile(string path) => LoadKeyFromFile(path, Kyrka.KeyLength, LoadSecret);

    /// <summary>Loads the 32-byte device key-encryption key used with a cathedral (kyrka_device_kek_load).</summary>
    public void LoadDeviceKek(ReadOnlySpan<byte> kek) =>
        LoadKey(kek, Kyrka.KeyLength, &NativeMethods.kyrka_device_kek_load, "kyrka_device_kek_load");

    /// <summary>Loads the device KEK from a file containing exactly 32 raw bytes.</summary>
    public void LoadDeviceKekFromFile(string path) => LoadKeyFromFile(path, Kyrka.KeyLength, LoadDeviceKek);

    /// <summary>Loads the 32-byte secret bound to our cathedral identity (kyrka_cathedral_secret_load).</summary>
    public void LoadCathedralSecret(ReadOnlySpan<byte> secret) =>
        LoadKey(secret, Kyrka.KeyLength, &NativeMethods.kyrka_cathedral_secret_load, "kyrka_cathedral_secret_load");

    /// <summary>Loads the cathedral secret from a file containing exactly 32 raw bytes.</summary>
    public void LoadCathedralSecretFromFile(string path) => LoadKeyFromFile(path, Kyrka.KeyLength, LoadCathedralSecret);

    /// <summary>
    /// Loads the 64-byte cathedral offer signing key (kyrka_cathedral_cosk_load).
    /// Required before any message can be sent to a cathedral.
    /// </summary>
    public void LoadCathedralSigningKey(ReadOnlySpan<byte> key) =>
        LoadKey(key, Kyrka.CathedralSigningKeyLength, &NativeMethods.kyrka_cathedral_cosk_load, "kyrka_cathedral_cosk_load");

    /// <summary>Loads the cathedral offer signing key from a file containing exactly 64 raw bytes.</summary>
    public void LoadCathedralSigningKeyFromFile(string path) =>
        LoadKeyFromFile(path, Kyrka.CathedralSigningKeyLength, LoadCathedralSigningKey);

    /// <summary>
    /// Sets the MTU used for overhead and shroud calculations (kyrka_mtu_size).
    /// Must be between <see cref="Kyrka.MinimumMtu"/> and <see cref="Kyrka.MaximumMtu"/>.
    /// </summary>
    public void SetMtu(int mtu)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mtu, Kyrka.MinimumMtu);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mtu, Kyrka.MaximumMtu);

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            Check(NativeMethods.kyrka_mtu_size(_handle, (ushort)mtu), "kyrka_mtu_size");
        }
    }

    /// <summary>
    /// Enables shroud, which hides the sanctum headers on the wire
    /// (kyrka_shroud_enable). It cannot be turned off again.
    /// </summary>
    public void EnableShroud()
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            Check(NativeMethods.kyrka_shroud_enable(_handle), "kyrka_shroud_enable");
        }
    }

    /// <summary>
    /// Tells the context whether traffic flows directly peer-to-peer (true)
    /// or is relayed by a cathedral (false), so internally generated packets
    /// are shrouded with the right key (kyrka_p2p_active).
    /// </summary>
    public void SetPeerToPeerActive(bool active)
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            Check(NativeMethods.kyrka_p2p_active(_handle, active ? 1 : 0), "kyrka_p2p_active");
        }
    }

    /// <summary>
    /// Drives the key exchange (kyrka_key_manage). Call this on every event
    /// loop tick. Key offers are sent through the purgatory handler.
    /// </summary>
    /// <exception cref="KyrkaException">
    /// No secret is loaded (<see cref="KyrkaError.NoSecret"/>) or no purgatory
    /// handler is set (<see cref="KyrkaError.NoCallback"/>).
    /// </exception>
    public void ManageKeys()
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            InvokeNative(NativeMethods.kyrka_key_manage(_handle), "kyrka_key_manage");
        }
    }

    /// <summary>
    /// Tells libkyrka the peer has timed out, which erases all keys and
    /// raises a <see cref="KeysErasedEvent"/> (kyrka_peer_timeout).
    /// </summary>
    public void NotifyPeerTimeout()
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            InvokeNative(NativeMethods.kyrka_peer_timeout(_handle), "kyrka_peer_timeout");
        }
    }

    /// <summary>
    /// Encrypts plaintext for the peer (kyrka_heaven_input). The ciphertext is
    /// delivered synchronously to the purgatory handler.
    /// </summary>
    /// <param name="plaintext">1 to <see cref="Kyrka.MaxPacketDataLength"/> bytes of data.</param>
    /// <param name="shroud">The shroud key to use; ignored unless shroud is enabled.</param>
    /// <exception cref="KyrkaException">
    /// For example <see cref="KyrkaError.NoTxKey"/> while the key exchange has not completed.
    /// </exception>
    public void HeavenInput(ReadOnlySpan<byte> plaintext, ShroudKey shroud = ShroudKey.Peer)
    {
        ValidatePacketLength(plaintext, nameof(plaintext));

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            SubmitPacket(plaintext, shroud, &NativeMethods.kyrka_packet_databuf,
                &NativeMethods.kyrka_heaven_input, "kyrka_heaven_input");
        }
    }

    /// <summary>
    /// Submits a packet received from the network (kyrka_purgatory_input).
    /// </summary>
    /// <remarks>
    /// Valid data packets are decrypted and delivered synchronously to the
    /// heaven handler. Key exchange and cathedral packets are consumed
    /// internally. Packets that fail authentication, are replayed or are too
    /// old are silently dropped, as the protocol requires.
    /// </remarks>
    /// <param name="ciphertext">1 to <see cref="Kyrka.MaxPacketDataLength"/> bytes as received.</param>
    /// <param name="shroud">The shroud key to use; ignored unless shroud is enabled.</param>
    public void PurgatoryInput(ReadOnlySpan<byte> ciphertext, ShroudKey shroud = ShroudKey.Peer)
    {
        ValidatePacketLength(ciphertext, nameof(ciphertext));

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            SubmitPacket(ciphertext, shroud, &NativeMethods.kyrka_packet_recvbuf,
                &NativeMethods.kyrka_purgatory_input, "kyrka_purgatory_input");
        }
    }

    /// <summary>
    /// Configures the use of a cathedral (kyrka_cathedral_config). This also
    /// marks the tunnel as not peer-to-peer until <see cref="SetPeerToPeerActive"/>
    /// says otherwise.
    /// </summary>
    /// <param name="config">The cathedral settings.</param>
    /// <param name="handler">Receives the packets that must be sent to the cathedral.</param>
    public void ConfigureCathedral(CathedralConfig config, CathedralPacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handler);

        var native = new CathedralConfigNative
        {
            UserData = _handle.UserData,
            FlockSource = config.FlockSource,
            FlockDestination = config.FlockDestination,
            Tunnel = config.Tunnel,
            Identity = config.Identity,
            Hops = config.Hops,
            Group = config.Group,
            Hidden = config.Hidden ? 1 : 0,
            Remembrance = config.Remembrance ? 1 : 0,
            Send = &OnNativeCathedralPacket,
        };

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            _cathedralHandler = handler;
            Check(NativeMethods.kyrka_cathedral_config(_handle, &native), "kyrka_cathedral_config");
        }
    }

    /// <summary>
    /// Sends a notification to the cathedral through the cathedral handler
    /// (kyrka_cathedral_notify). Do this periodically to stay registered.
    /// </summary>
    public void NotifyCathedral()
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            InvokeNative(NativeMethods.kyrka_cathedral_notify(_handle), "kyrka_cathedral_notify");
        }
    }

    /// <summary>
    /// Sends a NAT detection message through the cathedral handler
    /// (kyrka_cathedral_nat_detection).
    /// </summary>
    public void RequestNatDetection()
    {
        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            InvokeNative(NativeMethods.kyrka_cathedral_nat_detection(_handle), "kyrka_cathedral_nat_detection");
        }
    }

    /// <summary>
    /// Sends a liturgy request through the cathedral handler (kyrka_cathedral_liturgy).
    /// </summary>
    /// <param name="peers">
    /// Empty to be interested in all peers, or exactly <see cref="Kyrka.PeersPerFlock"/>
    /// bytes indicating which peers we are interested in.
    /// </param>
    public void SendLiturgy(ReadOnlySpan<byte> peers = default)
    {
        if (!peers.IsEmpty && peers.Length != Kyrka.PeersPerFlock)
        {
            throw new ArgumentException(
                $"Must be empty or exactly {Kyrka.PeersPerFlock} bytes.", nameof(peers));
        }

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            fixed (byte* pointer = peers)
            {
                InvokeNative(
                    NativeMethods.kyrka_cathedral_liturgy(_handle, peers.IsEmpty ? null : pointer, (nuint)peers.Length),
                    "kyrka_cathedral_liturgy");
            }
        }
    }

    /// <summary>Zeroizes and frees the native context.</summary>
    public void Dispose() => _handle.Dispose();

    private static void ValidatePacketLength(ReadOnlySpan<byte> data, string parameterName)
    {
        if (data.IsEmpty || data.Length > Kyrka.MaxPacketDataLength)
        {
            throw new ArgumentException(
                $"Must be between 1 and {Kyrka.MaxPacketDataLength} bytes, was {data.Length}.", parameterName);
        }
    }

    private static void LoadKeyFromFile(string path, int expectedLength, LoadKeyAction load)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Read in managed code so Unicode paths and error messages behave the
        // same on every platform; the bytes are wiped once they are loaded.
        // The size is checked first so a wrong path to a large file fails
        // without reading it, and again after reading in case it changed.
        ThrowIfWrongKeyFileLength(path, new FileInfo(path).Length, expectedLength);

        var key = File.ReadAllBytes(path);
        try
        {
            ThrowIfWrongKeyFileLength(path, key.Length, expectedLength);
            load(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void ThrowIfWrongKeyFileLength(string path, long length, int expectedLength)
    {
        if (length != expectedLength)
        {
            throw new InvalidDataException(
                $"Key file '{path}' must contain exactly {expectedLength} bytes, found {length}.");
        }
    }

    private void LoadKey(
        ReadOnlySpan<byte> key,
        int expectedLength,
        delegate*<KyrkaSafeHandle, byte*, nuint, int> nativeLoad,
        string operation)
    {
        if (key.Length != expectedLength)
        {
            throw new ArgumentException($"Must be exactly {expectedLength} bytes, was {key.Length}.", nameof(key));
        }

        lock (Kyrka.NativeLock)
        {
            ThrowIfDisposed();
            fixed (byte* pointer = key)
            {
                Check(nativeLoad(_handle, pointer, (nuint)key.Length), operation);
            }
        }
    }

    /// <summary>
    /// Copies data into a stack-allocated kyrka_packet and hands it to libkyrka.
    /// The packet buffer is wiped afterwards because it may hold plaintext.
    /// </summary>
    private void SubmitPacket(
        ReadOnlySpan<byte> data,
        ShroudKey shroud,
        delegate*<KyrkaSafeHandle, KyrkaPacketNative*, nuint*, byte*> getBuffer,
        delegate*<KyrkaSafeHandle, KyrkaPacketNative*, int> submit,
        string operation)
    {
        KyrkaPacketNative packet = default;
        try
        {
            nuint capacity;
            var buffer = getBuffer(_handle, &packet, &capacity);
            if (buffer == null)
            {
                throw LastError(operation);
            }

            if ((nuint)data.Length > capacity)
            {
                throw new ArgumentException($"Packet exceeds the {capacity} byte buffer.", nameof(data));
            }

            data.CopyTo(new Span<byte>(buffer, data.Length));
            packet.Length = (nuint)data.Length;
            packet.Shroud = (uint)shroud;

            InvokeNative(submit(_handle, &packet), operation);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(new Span<byte>(packet.Data, Kyrka.PacketBufferLength));
        }
    }

    /// <summary>
    /// Completes a native call that may have invoked handlers: an exception
    /// thrown by a handler takes precedence over the native result.
    /// </summary>
    private void InvokeNative(int result, string operation)
    {
        RethrowHandlerException();
        Check(result, operation);
    }

    private void Check(int result, string operation)
    {
        if (result == -1)
        {
            throw LastError(operation);
        }
    }

    private KyrkaException LastError(string operation) =>
        new((KyrkaError)NativeMethods.kyrka_last_error(_handle), operation);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);

    private delegate void LoadKeyAction(ReadOnlySpan<byte> key);
}
