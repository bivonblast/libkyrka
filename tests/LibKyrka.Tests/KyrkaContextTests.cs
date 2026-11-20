using System.Security.Cryptography;

namespace LibKyrka.Tests;

/// <summary>
/// Behavior of a single context: validation, error reporting and lifetime.
/// </summary>
public sealed class KyrkaContextTests : IDisposable
{
    private readonly KyrkaContext _context = new();

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(Kyrka.KeyLength - 1)]
    [InlineData(Kyrka.KeyLength + 1)]
    public void LoadSecret_WithWrongLength_ThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => _context.LoadSecret(new byte[length]));
    }

    [Fact]
    public void LoadSecret_WithCorrectLength_Succeeds()
    {
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));
    }

    [Fact]
    public void LoadDeviceKek_WithWrongLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _context.LoadDeviceKek(new byte[16]));
    }

    [Fact]
    public void LoadCathedralSecret_WithWrongLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _context.LoadCathedralSecret(new byte[16]));
    }

    [Fact]
    public void LoadCathedralSigningKey_RequiresSixtyFourBytes()
    {
        Assert.Throws<ArgumentException>(() => _context.LoadCathedralSigningKey(new byte[Kyrka.KeyLength]));
        _context.LoadCathedralSigningKey(RandomNumberGenerator.GetBytes(Kyrka.CathedralSigningKeyLength));
    }

    [Fact]
    public void LoadSecretFromFile_WithValidFile_Succeeds()
    {
        using var file = new TemporaryFile(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        _context.LoadSecretFromFile(file.Path);
    }

    [Fact]
    public void LoadSecretFromFile_WithWrongSize_ThrowsInvalidDataException()
    {
        using var file = new TemporaryFile(new byte[Kyrka.KeyLength + 1]);

        Assert.Throws<InvalidDataException>(() => _context.LoadSecretFromFile(file.Path));
    }

    [Fact]
    public void LoadSecretFromFile_WithMissingFile_ThrowsFileNotFoundException()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<FileNotFoundException>(() => _context.LoadSecretFromFile(missing));
    }

    [Theory]
    [InlineData(Kyrka.MinimumMtu - 1)]
    [InlineData(Kyrka.MaximumMtu + 1)]
    [InlineData(0)]
    public void SetMtu_OutOfRange_ThrowsArgumentOutOfRangeException(int mtu)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _context.SetMtu(mtu));
    }

    [Theory]
    [InlineData(Kyrka.MinimumMtu)]
    [InlineData(1422)]
    [InlineData(Kyrka.MaximumMtu)]
    public void SetMtu_InRange_Succeeds(int mtu)
    {
        _context.SetMtu(mtu);
    }

    [Fact]
    public void ManageKeys_WithoutSecret_ThrowsNoSecret()
    {
        _context.SetPurgatoryHandler((_, _) => { });

        var exception = Assert.Throws<KyrkaException>(_context.ManageKeys);

        Assert.Equal(KyrkaError.NoSecret, exception.Error);
        Assert.Equal("kyrka_key_manage", exception.Operation);
    }

    [Fact]
    public void ManageKeys_WithoutPurgatoryHandler_ThrowsNoCallback()
    {
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        var exception = Assert.Throws<KyrkaException>(_context.ManageKeys);

        Assert.Equal(KyrkaError.NoCallback, exception.Error);
    }

    [Fact]
    public void ManageKeys_WithSecretAndHandler_SendsKeyOffers()
    {
        var sent = new List<byte[]>();
        _context.SetPurgatoryHandler((packet, _) => sent.Add(packet.ToArray()));
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        // The first call creates an offer, the second sends it.
        _context.ManageKeys();
        _context.ManageKeys();

        Assert.NotEmpty(sent);
        Assert.All(sent, packet => Assert.InRange(packet.Length, 1, Kyrka.MaxPacketDataLength));
    }

    [Fact]
    public void ManageKeys_ReportsKeyExchangeProgress()
    {
        var events = new List<KyrkaEvent>();
        _context.EventReceived += events.Add;
        _context.SetPurgatoryHandler((_, _) => { });
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        _context.ManageKeys();

        var info = Assert.Single(events.OfType<ExchangeInfoEvent>());
        Assert.Equal("no keys", info.Reason);
    }

    [Fact]
    public void HandlerException_IsRethrownToTheCaller()
    {
        _context.SetPurgatoryHandler((_, _) => throw new InvalidOperationException("send failed"));
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        _context.ManageKeys();
        var exception = Assert.Throws<InvalidOperationException>(_context.ManageKeys);

        Assert.Equal("send failed", exception.Message);
    }

    [Fact]
    public void HandlerException_IsOnlyRethrownOnce()
    {
        var shouldThrow = true;
        _context.SetPurgatoryHandler((_, _) =>
        {
            if (shouldThrow)
            {
                shouldThrow = false;
                throw new InvalidOperationException();
            }
        });
        _context.LoadSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        _context.ManageKeys();
        Assert.Throws<InvalidOperationException>(_context.ManageKeys);

        _context.ManageKeys();
    }

    [Fact]
    public void HeavenInput_WithoutPurgatoryHandler_ThrowsNoCallback()
    {
        var exception = Assert.Throws<KyrkaException>(() => _context.HeavenInput("hello"u8));

        Assert.Equal(KyrkaError.NoCallback, exception.Error);
    }

    [Fact]
    public void HeavenInput_BeforeKeyExchange_ThrowsNoTxKey()
    {
        _context.SetPurgatoryHandler((_, _) => { });

        var exception = Assert.Throws<KyrkaException>(() => _context.HeavenInput("hello"u8));

        Assert.Equal(KyrkaError.NoTxKey, exception.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Kyrka.MaxPacketDataLength + 1)]
    public void HeavenInput_WithInvalidLength_ThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => _context.HeavenInput(new byte[length]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Kyrka.MaxPacketDataLength + 1)]
    public void PurgatoryInput_WithInvalidLength_ThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => _context.PurgatoryInput(new byte[length]));
    }

    [Fact]
    public void PurgatoryInput_WithTooShortPacket_IsSilentlyDropped()
    {
        var received = false;
        _context.SetHeavenHandler((_, _) => received = true);

        _context.PurgatoryInput(new byte[8]);

        Assert.False(received);
    }

    [Fact]
    public void NotifyPeerTimeout_RaisesKeysErasedEvent()
    {
        var events = new List<KyrkaEvent>();
        _context.EventReceived += events.Add;

        _context.NotifyPeerTimeout();

        Assert.IsType<KeysErasedEvent>(Assert.Single(events));
    }

    [Fact]
    public void ConfigureCathedral_WithZeroFlock_ThrowsParameterError()
    {
        var config = new CathedralConfig { FlockSource = 0, Tunnel = 0x0102, Identity = 0xbadf00d };

        var exception = Assert.Throws<KyrkaException>(() => _context.ConfigureCathedral(config, (_, _) => { }));

        Assert.Equal(KyrkaError.Parameter, exception.Error);
    }

    [Fact]
    public void NotifyCathedral_WithoutConfiguration_ThrowsNoConfig()
    {
        var exception = Assert.Throws<KyrkaException>(_context.NotifyCathedral);

        Assert.Equal(KyrkaError.NoConfig, exception.Error);
    }

    [Fact]
    public void NotifyCathedral_WithoutSigningKey_ThrowsNoCosk()
    {
        ConfigureCathedral((_, _) => { });
        _context.LoadCathedralSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));

        var exception = Assert.Throws<KyrkaException>(_context.NotifyCathedral);

        Assert.Equal(KyrkaError.NoCosk, exception.Error);
    }

    [Fact]
    public void CathedralMessages_AreDeliveredToTheCathedralHandler_WithTheirKind()
    {
        var sent = new List<(byte[] Packet, CathedralMessageKind Kind)>();
        ConfigureCathedral((packet, kind) => sent.Add((packet.ToArray(), kind)));
        LoadAllCathedralKeys();

        _context.NotifyCathedral();
        _context.RequestNatDetection();
        _context.SendLiturgy();

        // libkyrka sends liturgy requests as regular cathedral messages.
        Assert.Equal(
            [CathedralMessageKind.Regular, CathedralMessageKind.NatDetection, CathedralMessageKind.Regular],
            sent.Select(message => message.Kind));
        Assert.All(sent, message => Assert.NotEmpty(message.Packet));
    }

    [Fact]
    public void SendLiturgy_WithSpecificPeers_Succeeds()
    {
        var sent = 0;
        ConfigureCathedral((_, _) => sent++);
        LoadAllCathedralKeys();

        var peers = new byte[Kyrka.PeersPerFlock];
        peers[1] = 1;
        _context.SendLiturgy(peers);

        Assert.Equal(1, sent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Kyrka.PeersPerFlock - 1)]
    [InlineData(Kyrka.PeersPerFlock + 1)]
    public void SendLiturgy_WithWrongPeerCount_ThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => _context.SendLiturgy(new byte[length]));
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        _context.Dispose();
        _context.Dispose();

        Assert.True(_context.IsDisposed);
    }

    [Fact]
    public void Methods_AfterDispose_ThrowObjectDisposedException()
    {
        _context.Dispose();

        Assert.Throws<ObjectDisposedException>(_context.ManageKeys);
        Assert.Throws<ObjectDisposedException>(() => _context.HeavenInput("hello"u8));
        Assert.Throws<ObjectDisposedException>(() => _context.LoadSecret(new byte[Kyrka.KeyLength]));
    }

    [Fact]
    public void Handlers_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => _context.SetHeavenHandler(null!));
        Assert.Throws<ArgumentNullException>(() => _context.SetPurgatoryHandler(null!));
    }

    private void ConfigureCathedral(CathedralPacketHandler handler)
    {
        var config = new CathedralConfig
        {
            FlockSource = 0xdeadbeefcafebabe,
            Tunnel = 0x0102,
            Identity = 0xbadf00d,
        };

        _context.ConfigureCathedral(config, handler);
    }

    private void LoadAllCathedralKeys()
    {
        _context.LoadCathedralSecret(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));
        _context.LoadDeviceKek(RandomNumberGenerator.GetBytes(Kyrka.KeyLength));
        _context.LoadCathedralSigningKey(RandomNumberGenerator.GetBytes(Kyrka.CathedralSigningKeyLength));
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(byte[] contents)
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllBytes(Path, contents);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
