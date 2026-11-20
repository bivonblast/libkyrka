namespace LibKyrka.Tests;

public sealed class KyrkaTests
{
    [Fact]
    public void NativeVersion_LoadsNativeLibrary_AndIsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Kyrka.NativeVersion));
    }

    [Fact]
    public void NativeApiVersion_MatchesTheVersionTheBindingsWereWrittenFor()
    {
        // If this fails, the native library was upgraded and libkyrka.h must
        // be compared against the Interop folder before bumping ApiVersion.
        Assert.Equal(Kyrka.ApiVersion, Kyrka.NativeApiVersion);
    }
}
