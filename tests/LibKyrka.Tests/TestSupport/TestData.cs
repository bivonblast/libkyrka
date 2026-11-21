using System.Security.Cryptography;

namespace LibKyrka.Tests.TestSupport;

/// <summary>Test inputs, written so they work on every target framework.</summary>
internal static class TestData
{
    public static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using var generator = RandomNumberGenerator.Create();
        generator.GetBytes(bytes);
        return bytes;
    }

    /// <summary>An IPv4 address as libkyrka stores it: in network byte order, i.e. a.b.c.d in memory order.</summary>
    public static uint NetworkOrderAddress(byte a, byte b, byte c, byte d) =>
        BitConverter.ToUInt32(new[] { a, b, c, d }, 0);
}
