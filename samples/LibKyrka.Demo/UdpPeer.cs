using System.Net;
using System.Net.Sockets;
using System.Text;
using LibKyrka;

namespace LibKyrka.Demo;

/// <summary>
/// A sanctum tunnel endpoint over UDP: the typical way to use libkyrka.
/// </summary>
/// <remarks>
/// libkyrka only encrypts, decrypts and negotiates keys; this class provides
/// the networking. Call <see cref="Poll"/> in a loop: it feeds received
/// datagrams into the context and lets it manage keys.
/// </remarks>
internal sealed class UdpPeer : IDisposable
{
    private readonly string _name;
    private readonly Socket _socket;
    private readonly KyrkaContext _context = new();
    // Large enough for any UDP datagram, so oversized ones can be detected and dropped.
    private readonly byte[] _receiveBuffer = new byte[ushort.MaxValue];

    public UdpPeer(string name, IPEndPoint listen, ReadOnlySpan<byte> secret)
    {
        _name = name;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IgnoreUnreachablePeer(_socket);
        _socket.Bind(listen);

        _context.EventReceived += OnEvent;

        // Decrypted data from the peer.
        _context.SetHeavenHandler((plaintext, sequence) =>
            Log.Info(_name, $"received #{sequence}: \"{Encoding.UTF8.GetString(plaintext)}\""));

        // Encrypted data (and key offers) to put on the wire.
        _context.SetPurgatoryHandler((ciphertext, _) =>
        {
            if (Remote is not null)
            {
                _socket.SendTo(ciphertext, Remote);
            }
        });

        _context.SetMtu(1422);
        _context.LoadSecret(secret);
    }

    /// <summary>Where encrypted packets are sent. Nothing is sent while this is null.</summary>
    public IPEndPoint? Remote { get; set; }

    /// <summary>The address this peer receives on (useful when bound to port 0).</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    /// <summary>True once keys exist in both directions.</summary>
    public bool IsEstablished { get; private set; }

    /// <summary>
    /// One event loop tick: waits up to <paramref name="timeout"/> for
    /// datagrams, decrypts all that arrived, then manages keys.
    /// </summary>
    public void Poll(TimeSpan timeout)
    {
        var wait = timeout;
        while (_socket.Poll(wait, SelectMode.SelectRead))
        {
            var length = _socket.Receive(_receiveBuffer);
            Receive(_receiveBuffer.AsSpan(0, length));
            wait = TimeSpan.Zero;
        }

        _context.ManageKeys();
    }

    public void Send(string message)
    {
        _context.HeavenInput(Encoding.UTF8.GetBytes(message));
        Log.Info(_name, $"sent: \"{message}\"");
    }

    public void Dispose()
    {
        _context.Dispose();
        _socket.Dispose();
    }

    /// <summary>
    /// On Windows, a datagram sent to a port nobody listens on makes the next
    /// Receive throw ConnectionReset. The peer simply is not running yet, so
    /// turn that behavior off (SIO_UDP_CONNRESET).
    /// </summary>
    private static void IgnoreUnreachablePeer(Socket socket)
    {
        if (OperatingSystem.IsWindows())
        {
            const int SioUdpConnReset = unchecked((int)0x9800000C);
            socket.IOControl(SioUdpConnReset, [0, 0, 0, 0], null);
        }
    }

    private void Receive(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length > Kyrka.MaxPacketDataLength)
        {
            Log.Warn(_name, $"dropped a {datagram.Length} byte datagram, larger than any sanctum packet");
            return;
        }

        try
        {
            _context.PurgatoryInput(datagram);
        }
        catch (KyrkaException exception)
        {
            // E.g. data arriving before our keys are ready; drop it and carry on.
            Log.Warn(_name, exception.Message);
        }
    }

    private void OnEvent(KyrkaEvent kyrkaEvent)
    {
        switch (kyrkaEvent)
        {
            case KeysInfoEvent keys:
                Log.Event(_name, $"keys installed tx=0x{keys.TxSpi:x8} rx=0x{keys.RxSpi:x8} peer=0x{keys.PeerId:x}");
                if (keys.IsEstablished && !IsEstablished)
                {
                    IsEstablished = true;
                    Log.Info(_name, "tunnel established");
                }

                break;

            case KeysErasedEvent:
                IsEstablished = false;
                Log.Event(_name, "keys erased");
                break;

            case ExchangeInfoEvent exchange:
                Log.Event(_name, $"key exchange: {exchange.Reason}");
                break;

            case LogMessageEvent log:
                Log.Event(_name, $"libkyrka: {log.Message}");
                break;

            default:
                Log.Event(_name, kyrkaEvent.ToString());
                break;
        }
    }
}
